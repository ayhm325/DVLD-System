using Application.Common.Results;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data;
using System.Data.Common;

namespace Infrastructure.IntegrationTests;

public sealed partial class UnitOfWorkTests
{
    private sealed class FaultInjectingTransactionInterceptor(
        int? failAfterServerCommitOnAttempt = null,
        int? rollbackAndFailBeforeServerCommitOnAttempt = null,
        bool failOnRollback = false)
        : DbTransactionInterceptor
    {
        private int _committingCount;
        private int _committedCount;

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            var attempt = Interlocked.Increment(ref _committingCount);

            if (rollbackAndFailBeforeServerCommitOnAttempt == attempt)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new TimeoutException(
                    "Simulated transient failure before SQL Server committed the transaction.");
            }

            return result;
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var attempt = Interlocked.Increment(ref _committedCount);

            if (failAfterServerCommitOnAttempt == attempt)
            {
                throw new TimeoutException(
                    "Simulated transient connection drop after SQL Server committed the transaction.");
            }

            return Task.CompletedTask;
        }

        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (failOnRollback)
            {
                throw new InvalidOperationException(
                    "Simulated physical rollback failure from interceptor.");
            }

            return base.TransactionRollingBackAsync(transaction, eventData, result, cancellationToken);
        }
    }
}
