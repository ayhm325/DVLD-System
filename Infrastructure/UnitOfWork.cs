using Application.Common.Results;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Runtime.ExceptionServices;

namespace Infrastructure;

public sealed partial class UnitOfWork : IUnitOfWork
{
    private readonly DVLDDbContext _context;

    public UnitOfWork(DVLDDbContext context)
    {
        _context = context
            ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            TranslateAndRethrowIfKnownConstraint(ex);
            throw;
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNoActiveTransaction();

        var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        return new UnitOfWorkTransaction(
            _context,
            transaction,
            deferPhysicalCommit: false);
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        EnsureNoActiveTransaction();

        var transaction =
            await _context.Database.BeginTransactionAsync(
                isolationLevel,
                cancellationToken);

        return new UnitOfWorkTransaction(
            _context,
            transaction,
            deferPhysicalCommit: false);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<IUnitOfWorkTransaction, Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
        => ExecuteInTransactionCoreAsync(
            operation,
            verifySucceeded: null,
            isolationLevel,
            cancellationToken);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<IUnitOfWorkTransaction, Task<T>> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verifySucceeded);

        return ExecuteInTransactionCoreAsync(
            operation,
            verifySucceeded,
            isolationLevel,
            cancellationToken);
    }

    private async Task<T> ExecuteInTransactionCoreAsync<T>(
        Func<IUnitOfWorkTransaction, Task<T>> operation,
        Func<CancellationToken, Task<bool>>? verifySucceeded,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        EnsureNoActiveTransaction();

        var strategy = _context.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            if (++attempt > 1)
            {
                _context.ChangeTracker.Clear();
            }

            EnsureNoActiveTransaction();

            using var tracker = new TransactionVerificationTracker(_context);

            var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    isolationLevel,
                    cancellationToken);

            await using var transaction =
                new UnitOfWorkTransaction(
                    _context,
                    dbTransaction,
                    deferPhysicalCommit: true);

            var commitStageReached = false;

            try
            {
                var result = await operation(transaction);

                if ((result is Result { IsFailure: true } && !transaction.IsCommitRequested) ||
                    transaction.IsRolledBack)
                {
                    if (!transaction.IsRolledBack)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    _context.ChangeTracker.Clear();

                    return result;
                }

                if (!transaction.IsCommitRequested)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    try
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException ex)
                    {
                        TranslateAndRethrowIfKnownConstraint(ex);
                        throw;
                    }
                }

                tracker.Detach();
                commitStageReached = true;

                try
                {
                    await transaction.CommitPhysicalAsync(cancellationToken);
                    return result;
                }
                catch (Exception commitException)
                {
                    await transaction.DisposeAsync();
                    _context.ChangeTracker.Clear();

                    var committed = await VerifyCommitOutcomeAsync(
                        verifySucceeded,
                        tracker,
                        commitException);

                    if (committed)
                    {
                        return result;
                    }

                    ExceptionDispatchInfo.Capture(commitException).Throw();
                    throw;
                }
            }
            catch (Exception ex) when (!commitStageReached)
            {
                try
                {
                    if (!transaction.IsRolledBack)
                    {
                        await transaction.RollbackPhysicalAsync(CancellationToken.None);
                    }
                }
                catch (Exception rollbackEx)
                {
                    // Preserve the original exception while recording the rollback failure.
                    ex.Data["RollbackException"] = rollbackEx.Message;
                }
                finally
                {
                    _context.ChangeTracker.Clear();
                }

                throw;
            }
        });
    }

    private void EnsureNoActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already active on this DbContext instance. Nested transactions are not supported.");
        }
    }
}
