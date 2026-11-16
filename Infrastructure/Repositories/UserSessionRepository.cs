using System.Data;
using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Repositories;

public sealed class UserSessionRepository(DVLDDbContext context)
    : Application.Interfaces.IUserSessionRepository
{
    private readonly DVLDDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));

    public Task<UserSession?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                session => session.RefreshTokenHash == tokenHash,
                cancellationToken);

    public async Task<bool> TryConsumeAsync(
        Guid sessionId,
        DateTime nowUtc,
        Guid replacedBySessionId,
        CancellationToken cancellationToken = default)
    {
        // The conditional UPDATE is the concurrency arbiter: a second caller blocks
        // on the row lock and then observes RevokedAtUtc != null, affecting 0 rows.
        var affected = await _context.UserSessions
            .Where(session =>
                session.Id == sessionId &&
                session.RevokedAtUtc == null &&
                session.ExpiresAtUtc > nowUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        session => session.RevokedAtUtc,
                        nowUtc)
                    .SetProperty(
                        session => session.RevocationReason,
                        SessionRevocationReasons.Rotated)
                    .SetProperty(
                        session => session.ReplacedBySessionId,
                        replacedBySessionId),
                cancellationToken);

        return affected > 0;
    }

    public async Task AddAsync(
        UserSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _context.UserSessions.AddAsync(session, cancellationToken);
    }

    public Task<int> RevokeFamilyAsync(
        Guid tokenFamilyId,
        DateTime nowUtc,
        string reason,
        CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .Where(session =>
                session.TokenFamilyId == tokenFamilyId &&
                session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        session => session.RevokedAtUtc,
                        nowUtc)
                    .SetProperty(
                        session => session.RevocationReason,
                        reason),
                cancellationToken);

    public Task<int> RevokeAllForUserAsync(
        int userId,
        DateTime nowUtc,
        string reason,
        CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .Where(session =>
                session.UserId == userId &&
                session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        session => session.RevokedAtUtc,
                        nowUtc)
                    .SetProperty(
                        session => session.RevocationReason,
                        reason),
                cancellationToken);

    public Task<bool> HasActiveSessionInFamilyAsync(
        Guid tokenFamilyId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .AsNoTracking()
            .AnyAsync(
                session =>
                    session.TokenFamilyId == tokenFamilyId &&
                    session.RevokedAtUtc == null &&
                    session.ExpiresAtUtc > nowUtc,
                cancellationToken);

    public Task<int> RevokeByTokenHashAsync(
        string tokenHash,
        DateTime nowUtc,
        string reason,
        CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .Where(session =>
                session.RefreshTokenHash == tokenHash &&
                session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        session => session.RevokedAtUtc,
                        nowUtc)
                    .SetProperty(
                        session => session.RevocationReason,
                        reason),
                cancellationToken);

    public async Task<bool> AcquireFamilyLockAsync(
        Guid tokenFamilyId,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default) =>
        await AcquireLockCoreAsync($"DVLD_TokenFamily_{tokenFamilyId}", timeoutMs, cancellationToken);

    public async Task<bool> AcquireUserSessionsLockAsync(
        int userId,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default) =>
        await AcquireLockCoreAsync($"DVLD_UserSessions_{userId}", timeoutMs, cancellationToken);

    private async Task<bool> AcquireLockCoreAsync(
        string resourceName,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var transaction = _context.Database.CurrentTransaction;
        if (transaction is null)
        {
            // Fail closed: the lock is transaction-owned; an active transaction is mandatory.
            return false;
        }

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = Math.Max(30, (timeoutMs / 1000) + 5);

        var resource = command.CreateParameter();
        resource.ParameterName = "@Resource";
        resource.Value = resourceName;
        command.Parameters.Add(resource);

        var mode = command.CreateParameter();
        mode.ParameterName = "@LockMode";
        mode.Value = "Exclusive";
        command.Parameters.Add(mode);

        var owner = command.CreateParameter();
        owner.ParameterName = "@LockOwner";
        owner.Value = "Transaction";
        command.Parameters.Add(owner);

        var timeout = command.CreateParameter();
        timeout.ParameterName = "@LockTimeout";
        timeout.Value = timeoutMs;
        command.Parameters.Add(timeout);

        var resultParam = command.CreateParameter();
        resultParam.ParameterName = "@Result";
        resultParam.Direction = ParameterDirection.ReturnValue;
        command.Parameters.Add(resultParam);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }

        var returnCode = resultParam.Value is int value ? value : -999;

        // sp_getapplock return codes:
        //  0 : Granted synchronously
        //  1 : Granted synchronously after waiting for other locks
        // -1 : Lock request timed out
        // -2 : Lock request was canceled
        // -3 : Lock request was chosen as a deadlock victim
        // -999 : General call/parameter validation error
        return returnCode >= 0;
    }
}
