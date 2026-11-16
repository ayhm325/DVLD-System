using System.Data;

using Application.Common.Results;
using Application.DTOs.AuthDTO;
using Application.DTOs.UserDTO;
using Application.Interfaces;
using Application.Options;
using Application.Validators;

using Domain.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Owns the authentication lifecycle: login, refresh-token rotation with reuse
/// detection, logout, and session issuance after security-sensitive changes.
/// </summary>
public sealed class AuthService(
    IUserRepository userRepository,
    IUserSessionRepository sessionRepository,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork,
    IJordanClock clock,
    IOptions<AuthSessionOptions> sessionOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly IUserRepository _userRepository =
        userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IUserSessionRepository _sessionRepository =
        sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    private readonly IJwtTokenService _jwtTokenService =
        jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
    private readonly IRefreshTokenService _refreshTokenService =
        refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
    private readonly IUnitOfWork _unitOfWork =
        unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IJordanClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly AuthSessionOptions _options =
        sessionOptions?.Value ?? throw new ArgumentNullException(nameof(sessionOptions));
    private readonly ILogger<AuthService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<LoginResponseDto>> LoginAsync(
        LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        var validation = UserValidator.ValidateLogin(dto);

        if (validation.IsFailure)
        {
            return Result<LoginResponseDto>.FromValidationFailure(validation.Error);
        }

        var username = dto.UserName.Trim();

        var user = await _userRepository.GetUserByUsernameAsync(
            username, cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
        {
            // Deliberately identical for unknown user, inactive user, and wrong
            // password: no account enumeration, no credential material in the log.
            _logger.LogWarning(
                "Failed login attempt for user name {UserName}.",
                username);

            return Result<LoginResponseDto>.FromUnauthorized(
                "Invalid username or password.");
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<Result<LoginResponseDto>> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!_refreshTokenService.LooksLikeValidToken(refreshToken))
        {
            return Unauthorized();
        }

        var now = _clock.UtcNow;
        var tokenHash = _refreshTokenService.HashToken(refreshToken);

        // Pre-transaction lookup strictly to identify candidate session and target lock keys.
        var session = await _sessionRepository.GetByTokenHashAsync(
            tokenHash, cancellationToken);

        if (session is null)
        {
            return Unauthorized();
        }

        var newRawToken = _refreshTokenService.CreateToken();

        var newSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = session.UserId,
            RefreshTokenHash = _refreshTokenService.HashToken(newRawToken),
            TokenFamilyId = session.TokenFamilyId,
            CreatedAtUtc = now,
            // Absolute family expiry is never extended by rotation.
            ExpiresAtUtc = session.ExpiresAtUtc,
            LastUsedAtUtc = now
        };

        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    // 1. Acquire exclusive SQL Server application lock for the user's sessions first.
                    // Lock acquisition order is strictly: UserSessions -> TokenFamily.
                    var userLockAcquired = await _sessionRepository.AcquireUserSessionsLockAsync(
                        session.UserId,
                        5000,
                        cancellationToken);

                    if (!userLockAcquired)
                    {
                        _logger.LogWarning(
                            "Failed to acquire user sessions application lock for user {UserId} during refresh.",
                            session.UserId);

                        return Result<LoginResponseDto>.FromConflict(
                            "Authentication session synchronization timed out. Retry the request.");
                    }

                    // 2. Acquire exclusive SQL Server application lock for this token family.
                    var lockAcquired = await _sessionRepository.AcquireFamilyLockAsync(
                        session.TokenFamilyId,
                        5000,
                        cancellationToken);

                    if (!lockAcquired)
                    {
                        _logger.LogWarning(
                            "Failed to acquire token family application lock for family {TokenFamilyId} during refresh for user {UserId}.",
                            session.TokenFamilyId,
                            session.UserId);

                        return Result<LoginResponseDto>.FromConflict(
                            "Authentication session synchronization timed out. Retry the request.");
                    }

                    // 3. Revalidate session state under the acquired locks.
                    var currentSession = await _sessionRepository.GetByTokenHashAsync(
                        tokenHash, cancellationToken);

                    if (currentSession is null)
                    {
                        return Unauthorized();
                    }

                    // Revalidate user state under the locks.
                    var currentUser = await _userRepository.GetUserByUserIdAsync(
                        currentSession.UserId, cancellationToken);

                    if (currentUser is null || !currentUser.IsActive)
                    {
                        await _sessionRepository.RevokeAllForUserAsync(
                            currentSession.UserId,
                            now,
                            SessionRevocationReasons.AccountDisabled,
                            cancellationToken);

                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        return Unauthorized();
                    }

                    // 4. Handle consumed / revoked token under the locks.
                    if (currentSession.RevokedAtUtc is not null)
                    {
                        return await HandleConsumedTokenAsync(
                            currentSession, now, transaction, cancellationToken);
                    }

                    // 5. Handle expired / idle-timed-out session under the locks.
                    if (currentSession.ExpiresAtUtc <= now ||
                        now - currentSession.LastUsedAtUtc >
                        TimeSpan.FromDays(Math.Max(1, _options.IdleTimeoutDays)))
                    {
                        await _sessionRepository.RevokeFamilyAsync(
                            currentSession.TokenFamilyId,
                            now,
                            SessionRevocationReasons.Expired,
                            cancellationToken);

                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        return Unauthorized();
                    }

                    // 6. Single conditional UPDATE: consume the current generation.
                    var consumed = await _sessionRepository.TryConsumeAsync(
                        currentSession.Id,
                        now,
                        newSession.Id,
                        cancellationToken);

                    if (!consumed)
                    {
                        return Result<LoginResponseDto>.FromConflict(
                            "A token refresh is already in progress. Retry the request.");
                    }

                    await _sessionRepository.AddAsync(newSession, cancellationToken);

                    if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
                    {
                        return Result<LoginResponseDto>.FromFailure(
                            "Failed to persist the refreshed authentication session.");
                    }

                    await transaction.CommitAsync(cancellationToken);

                    return Result<LoginResponseDto>.Success(
                        BuildResponse(currentUser, newRawToken, newSession.ExpiresAtUtc, newSession.TokenFamilyId));
                },
                IsolationLevel.ReadCommitted,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Refresh-token rotation failed for user {UserId}.",
                session.UserId);

            return Result<LoginResponseDto>.FromFailure(
                "Failed to rotate the authentication session.");
        }
    }

    public Task<Result> LogoutAsync(
        string? refreshToken, CancellationToken cancellationToken = default) =>
        LogoutAsync(refreshToken, null, cancellationToken);

    public async Task<Result> LogoutAsync(
        string? refreshToken, Guid? sessionId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        Guid? revokedFamilyId = null;

        if (_refreshTokenService.LooksLikeValidToken(refreshToken))
        {
            var tokenHash = _refreshTokenService.HashToken(refreshToken!);
            var session = await _sessionRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (session is not null)
            {
                var familyId = session.TokenFamilyId;
                revokedFamilyId = familyId;

                var result = await _unitOfWork.ExecuteInTransactionAsync(
                    async transaction =>
                    {
                        var lockAcquired = await _sessionRepository.AcquireFamilyLockAsync(
                            familyId,
                            5000,
                            cancellationToken);

                        if (!lockAcquired)
                        {
                            _logger.LogWarning(
                                "Failed to acquire token family application lock for family {TokenFamilyId} during logout.",
                                familyId);

                            return Result.Conflict(
                                "Failed to acquire session lock during logout. Retry the request.");
                        }

                        // When a refresh token is presented at logout, revoke its entire family.
                        // This ensures that if a concurrent rotation just consumed this generation
                        // and issued a replacement generation, the replacement is also revoked.
                        await _sessionRepository.RevokeFamilyAsync(
                            familyId,
                            now,
                            SessionRevocationReasons.Logout,
                            cancellationToken);

                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return Result.Success();
                    },
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                if (!result.IsSuccess)
                {
                    return result;
                }
            }
            else
            {
                await _sessionRepository.RevokeByTokenHashAsync(
                    tokenHash,
                    now,
                    SessionRevocationReasons.Logout,
                    cancellationToken);
            }
        }

        if (sessionId.HasValue && sessionId.Value != Guid.Empty && sessionId.Value != revokedFamilyId)
        {
            var familyId = sessionId.Value;

            var result = await _unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    var lockAcquired = await _sessionRepository.AcquireFamilyLockAsync(
                        familyId,
                        5000,
                        cancellationToken);

                    if (!lockAcquired)
                    {
                        _logger.LogWarning(
                            "Failed to acquire token family application lock for family {TokenFamilyId} during sessionId logout.",
                            familyId);

                        return Result.Conflict(
                            "Failed to acquire session lock during logout. Retry the request.");
                    }

                    await _sessionRepository.RevokeFamilyAsync(
                        familyId,
                        now,
                        SessionRevocationReasons.Logout,
                        cancellationToken);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return Result.Success();
                },
                IsolationLevel.ReadCommitted,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return Result.Success();
    }

    public async Task<Result> LogoutAllAsync(
        int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.Unauthorized(
                "Authenticated user is required.");
        }

        var user = await _userRepository.GetUserForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Unauthorized("User not found.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var lockAcquired = await _sessionRepository.AcquireUserSessionsLockAsync(
                    userId,
                    5000,
                    cancellationToken);

                if (!lockAcquired)
                {
                    _logger.LogWarning(
                        "Failed to acquire user sessions lock for user {UserId} during logout-all.",
                        userId);

                    return Result.Conflict(
                        "Failed to acquire user session lock during logout-all. Retry the request.");
                }

                user.SecurityStamp = Guid.NewGuid().ToString("N");

                await _sessionRepository.RevokeAllForUserAsync(
                    userId,
                    _clock.UtcNow,
                    SessionRevocationReasons.LogoutAll,
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            },
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    public async Task<Result<LoginResponseDto>> StartSessionAsync(
        int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetUserByUserIdAsync(
            userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<bool> IsPrincipalCurrentAsync(
        int userId, string securityStamp, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(securityStamp))
        {
            return false;
        }

        var state = await _userRepository.GetSecurityStateAsync(
            userId, cancellationToken);

        return state is not null &&
               state.IsActive &&
               string.Equals(
                   state.SecurityStamp,
                   securityStamp,
                   StringComparison.Ordinal);
    }

    public async Task<bool> IsPrincipalCurrentAsync(
        int userId, string securityStamp, Guid? sessionId, CancellationToken cancellationToken = default)
    {
        if (!sessionId.HasValue || sessionId.Value == Guid.Empty)
        {
            return await IsPrincipalCurrentAsync(userId, securityStamp, cancellationToken);
        }

        if (userId <= 0 || string.IsNullOrWhiteSpace(securityStamp))
        {
            return false;
        }

        var state = await _userRepository.GetSecurityStateAsync(
            userId, sessionId, _clock.UtcNow, cancellationToken);

        return state is not null &&
               state.IsActive &&
               string.Equals(
                   state.SecurityStamp,
                   securityStamp,
                   StringComparison.Ordinal) &&
               state.IsSessionActive;
    }

    private async Task<Result<LoginResponseDto>> HandleConsumedTokenAsync(
        UserSession session,
        DateTime now,
        IUnitOfWorkTransaction transaction,
        CancellationToken cancellationToken)
    {
        var revokedAt = session.RevokedAtUtc!.Value;

        var isBenignConcurrentRefresh =
            string.Equals(
                session.RevocationReason,
                SessionRevocationReasons.Rotated,
                StringComparison.Ordinal) &&
            now - revokedAt <=
            TimeSpan.FromSeconds(Math.Max(1, _options.RotationGraceSeconds)) &&
            await _sessionRepository.HasActiveSessionInFamilyAsync(
                session.TokenFamilyId, now, cancellationToken);

        if (isBenignConcurrentRefresh)
        {
            // A legitimate client raced itself (two tabs). No family revocation: the
            // winning rotation is still active, so the client can retry and receive
            // its already-issued cookie.
            return Result<LoginResponseDto>.FromConflict(
                "A token refresh is already in progress. Retry the request.");
        }

        // Confirmed reuse of a consumed generation: revoke the whole family.
        await _sessionRepository.RevokeFamilyAsync(
            session.TokenFamilyId,
            now,
            SessionRevocationReasons.ReuseDetected,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogWarning(
            "Refresh-token reuse detected for user {UserId}. Token family {TokenFamilyId} was revoked.",
            session.UserId,
            session.TokenFamilyId);

        return Unauthorized();
    }

    private async Task<Result<LoginResponseDto>> CreateSessionAsync(
        User user, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var rawToken = _refreshTokenService.CreateToken();

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            RefreshTokenHash = _refreshTokenService.HashToken(rawToken),
            TokenFamilyId = Guid.NewGuid(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(
                Math.Max(1, _options.RefreshTokenLifetimeDays)),
            LastUsedAtUtc = now
        };

        await _sessionRepository.AddAsync(session, cancellationToken);

        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            return Result<LoginResponseDto>.FromFailure(
                "Failed to create the authentication session.");
        }

        return Result<LoginResponseDto>.Success(
            BuildResponse(user, rawToken, session.ExpiresAtUtc, session.TokenFamilyId));
    }

    private LoginResponseDto BuildResponse(
        User user,
        string refreshToken,
        DateTime refreshExpiresAtUtc,
        Guid? sessionId = null)
    {
        var fullName = user.Person?.FullName ?? user.UserName;

        var token = _jwtTokenService.GenerateToken(
            new JwtTokenRequest
            {
                UserId = user.UserId,
                UserName = user.UserName,
                FullName = fullName,
                PersonId = user.PersonId,
                Role = user.Role,
                SecurityStamp = user.SecurityStamp,
                SessionId = sessionId
            });

        return new LoginResponseDto
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UserId = user.UserId,
            UserName = user.UserName,
            PersonId = user.PersonId,
            FullName = fullName,
            Role = user.Role,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpiresAtUtc
        };
    }

    private static Result<LoginResponseDto> Unauthorized() =>
        Result<LoginResponseDto>.FromUnauthorized(
            "The authentication session is invalid or has expired.");
}
