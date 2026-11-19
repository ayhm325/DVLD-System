using System.Data;

using Application.Common.Exceptions;
using Application.Common.Results;
using Application.DTOs.UserDTO;
using Application.Interfaces;
using Application.Mappings;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public sealed partial class UserService
{
    public async Task<Result<int>>
        AddUserAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        if (IsNonAdminAuthenticatedCaller())
        {
            return Result<int>.FromForbidden(
                "Administrator privileges are required.");
        }

        if (dto is null)
        {
            return Result<int>
                .FromValidationFailure(
                    "User data is required.");
        }

        var validation =
            UserValidator
                .ValidateCreateUser(dto);

        if (validation.IsFailure)
        {
            return Result<int>
                .FromValidationFailure(
                    validation.Error);
        }

        var username =
            dto.UserName.Trim();

        if (await _userRepository
                .IsUsernameTakenAsync(username, cancellationToken))
        {
            return Result<int>
                .FromConflict(
                    "Username is already in use.");
        }

        if (await _userRepository
                .IsUserExistsByPersonIdAsync(
                    dto.PersonId,
                    cancellationToken))
        {
            return Result<int>
                .FromConflict(
                    "This person is already associated with a user account.");
        }

        var hashedPassword =
            BCrypt.Net.BCrypt
                .HashPassword(
                    dto.Password);

        var user =
            UserMapper.ToEntity(
                dto,
                hashedPassword);

        await _userRepository
            .AddUserAsync(user, cancellationToken);

        try
        {
            var saved =
                await _unitOfWork
                    .SaveChangesAsync(cancellationToken);

            if (saved <= 0 ||
                user.UserId <= 0)
            {
                return Result<int>
                    .FromFailure(
                        "Failed to create user.");
            }

            return Result<int>
                .Success(
                    user.UserId);
        }
        catch (DuplicateUsernameException)
        {
            return Result<int>
                .FromConflict(
                    "Username is already in use.");
        }
        catch (DuplicatePersonUserException)
        {
            return Result<int>
                .FromConflict(
                    "This person is already associated with a user account.");
        }
    }

    public async Task<Result>
        UpdateUserAsync(
            int id,
            UpdateUserDto dto,
            CancellationToken cancellationToken = default)
    {
        if (IsNonAdminAuthenticatedCaller())
        {
            return Result.Forbidden(
                "Administrator privileges are required.");
        }

        if (id <= 0)
        {
            return Result.ValidationFailure(
                "Invalid user ID.");
        }

        if (dto is null)
        {
            return Result.ValidationFailure(
                "User data is required.");
        }

        var validation =
            UserValidator
                .ValidateUpdateUser(dto);

        if (validation.IsFailure)
        {
            return Result.ValidationFailure(
                validation.Error);
        }

        var user =
            await _userRepository
                .GetUserForUpdateAsync(id, cancellationToken);

        if (user is null)
        {
            return Result.NotFound(
                "User not found.");
        }

        // Account safety rules
        var targetRole = dto.Role ?? user.Role;
        var isRoleChange = targetRole != user.Role;

        if (id == 2 && user.Role == UserRole.Admin)
        {
            if (!dto.IsActive)
            {
                return Result.Forbidden(
                    "The primary administrator account cannot be deactivated.");
            }

            if (isRoleChange)
            {
                return Result.Forbidden(
                    "The primary administrator account role cannot be changed.");
            }

            if (string.Equals(user.UserName, "ayhm", StringComparison.OrdinalIgnoreCase) &&
                (!string.Equals(user.UserName, dto.UserName.Trim(), StringComparison.Ordinal) ||
                 user.PersonId != dto.PersonId))
            {
                return Result.Forbidden(
                    "The primary administrator account cannot be modified.");
            }
        }

        var isSelf =
            _currentUserService is { IsLoggedIn: true } &&
            _currentUserService.UserId == id;

        if (isSelf && !dto.IsActive)
        {
            return Result.Forbidden(
                "You cannot deactivate your own administrator account.");
        }

        if (isSelf && isRoleChange)
        {
            return Result.Forbidden(
                "You cannot change your own role.");
        }

        var removesActiveAdmin =
            user.Role == UserRole.Admin &&
            user.IsActive &&
            (!dto.IsActive || targetRole != UserRole.Admin);

        if (removesActiveAdmin)
        {
            var activeAdminCount = await _userRepository.GetActiveAdminCountAsync(cancellationToken);
            if (activeAdminCount <= 1)
            {
                return Result.Conflict(
                    !dto.IsActive
                        ? "Cannot deactivate the last active administrator."
                        : "Cannot change the role of the last active administrator.");
            }
        }

        var username =
            dto.UserName.Trim();

        if (await _userRepository
                .IsUsernameTakenForAnotherUserAsync(
                    username,
                    id,
                    cancellationToken))
        {
            return Result.Conflict(
                "Username is already in use by another user.");
        }

        if (user.PersonId != dto.PersonId &&
            await _userRepository
                .IsUserExistsByPersonIdAsync(
                    dto.PersonId,
                    cancellationToken))
        {
            return Result.Conflict(
                "This person is already associated with another user account.");
        }

        bool hasChanges =
            !string.Equals(user.UserName, username, StringComparison.Ordinal) ||
            user.PersonId != dto.PersonId ||
            user.IsActive != dto.IsActive ||
            isRoleChange;

        if (!hasChanges)
        {
            return Result.Success();
        }

        var securityStateChanged =
            isRoleChange ||
            user.IsActive != dto.IsActive;

        user.UserName =
            username;

        user.PersonId =
            dto.PersonId;

        user.IsActive =
            dto.IsActive;

        user.Role =
            targetRole;

        if (securityStateChanged)
        {
            // A rotated stamp invalidates every access token issued with the old
            // state. Session revocation happens inside the mutation boundary below.
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        try
        {
            if (removesActiveAdmin)
            {
                // The sequential count check above is only a fast path. The
                // authoritative check runs under a cross-process application lock so
                // two concurrent demotions cannot both observe two active admins.
                return await _unitOfWork.ExecuteInTransactionAsync(
                    async transaction =>
                    {
                        if (!await _userRepository.AcquireAdminMutationLockAsync(
                                cancellationToken))
                        {
                            return Result.Conflict(
                                "Another administrator change is in progress. Please retry.");
                        }

                        var activeAdminCount =
                            await _userRepository.GetActiveAdminCountAsync(
                                cancellationToken);

                        if (activeAdminCount <= 1)
                        {
                            return Result.Conflict(
                                !dto.IsActive
                                    ? "Cannot deactivate the last active administrator."
                                    : "Cannot change the role of the last active administrator.");
                        }

                        if (securityStateChanged)
                        {
                            await _sessionRepository.RevokeAllForUserAsync(
                                id,
                                _clock.UtcNow,
                                SessionRevocationReasons.SecurityStateChanged,
                                cancellationToken);
                        }

                        var savedInside =
                            await _unitOfWork.SaveChangesAsync(cancellationToken);

                        if (savedInside <= 0)
                        {
                            return Result.Failure(
                                "No user changes were saved.");
                        }

                        return Result.Success();
                    },
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
            }

            if (securityStateChanged)
            {
                return await _unitOfWork.ExecuteInTransactionAsync(
                    async transaction =>
                    {
                        await _sessionRepository.RevokeAllForUserAsync(
                            id,
                            _clock.UtcNow,
                            SessionRevocationReasons.SecurityStateChanged,
                            cancellationToken);

                        var savedInside =
                            await _unitOfWork.SaveChangesAsync(cancellationToken);

                        if (savedInside <= 0)
                        {
                            return Result.Failure(
                                "No user changes were saved.");
                        }

                        return Result.Success();
                    },
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
            }

            var saved =
                await _unitOfWork
                    .SaveChangesAsync(cancellationToken);

            if (saved <= 0)
            {
                return Result.Failure(
                    "No user changes were saved.");
            }

            return Result.Success();
        }
        catch (DuplicateUsernameException)
        {
            return Result.Conflict(
                "Username is already in use by another user.");
        }
        catch (DuplicatePersonUserException)
        {
            return Result.Conflict(
                "This person is already associated with another user account.");
        }
    }

    public async Task<Result>
        DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        if (IsNonAdminAuthenticatedCaller())
        {
            return Result.Forbidden(
                "Administrator privileges are required.");
        }

        if (id <= 0)
        {
            return Result.ValidationFailure(
                "Invalid user ID.");
        }

        var user =
            await _userRepository
                .GetUserForUpdateAsync(id, cancellationToken);

        if (user is null)
        {
            return Result.NotFound(
                "User not found.");
        }

        // Account safety rules
        if (id == 2 && user.Role == UserRole.Admin)
        {
            return Result.Forbidden(
                "The primary administrator account cannot be deleted.");
        }

        if (_currentUserService != null && _currentUserService.IsLoggedIn && _currentUserService.UserId == id)
        {
            return Result.Forbidden(
                "You cannot delete your own administrator account.");
        }

        if (user.Role == UserRole.Admin)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    if (!await _userRepository.AcquireAdminMutationLockAsync(
                            cancellationToken))
                    {
                        return Result.Conflict(
                            "Another administrator change is in progress. Please retry.");
                    }

                    var activeAdminCount =
                        await _userRepository.GetActiveAdminCountAsync(
                            cancellationToken);

                    if (activeAdminCount <= 1)
                    {
                        return Result.Conflict(
                            "Cannot delete the last administrator account.");
                    }

                    return await DeleteAndRevokeAsync(
                        user, transaction, cancellationToken);
                },
                IsolationLevel.ReadCommitted,
                cancellationToken);
        }

        return await DeleteAndRevokeAsync(
            user, transaction: null, cancellationToken);
    }

    private async Task<Result> DeleteAndRevokeAsync(
        User user,
        IUnitOfWorkTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is null)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(
                tx => DeleteAndRevokeAsync(user, tx, cancellationToken),
                IsolationLevel.ReadCommitted,
                cancellationToken);
        }

        _userRepository.DeleteUser(user);

        // The FK cascades, but revoke explicitly first so an in-flight refresh can
        // never observe an active session during deletion.
        await _sessionRepository.RevokeAllForUserAsync(
            user.UserId,
            _clock.UtcNow,
            SessionRevocationReasons.SecurityStateChanged,
            cancellationToken);

        var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved <= 0)
        {
            return Result.Failure("Failed to save user deletion.");
        }

        return Result.Success();
    }
}
