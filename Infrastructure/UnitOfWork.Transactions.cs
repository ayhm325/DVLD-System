using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure;

public sealed partial class UnitOfWork
{
    private sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly DVLDDbContext _context;
        private readonly IDbContextTransaction _transaction;
        private readonly bool _deferPhysicalCommit;

        private bool _isCommitRequested;
        private bool _isPhysicallyCommitted;
        private bool _isRolledBack;
        private bool _isDisposed;

        public UnitOfWorkTransaction(
            DVLDDbContext context,
            IDbContextTransaction transaction,
            bool deferPhysicalCommit)
        {
            _context = context
                ?? throw new ArgumentNullException(nameof(context));

            _transaction = transaction
                ?? throw new ArgumentNullException(nameof(transaction));

            _deferPhysicalCommit = deferPhysicalCommit;
        }

        public bool IsCommitRequested => _isCommitRequested;

        public bool IsPhysicallyCommitted => _isPhysicallyCommitted;

        public bool IsRolledBack => _isRolledBack;

        public bool IsCompleted => _isPhysicallyCommitted || _isRolledBack;

        public async Task CommitAsync(
            CancellationToken cancellationToken = default)
        {
            if (_isRolledBack)
            {
                throw new InvalidOperationException(
                    "Cannot commit a transaction that has already been rolled back.");
            }

            if (_isCommitRequested)
                return;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                TranslateAndRethrowIfKnownConstraint(ex);
                throw;
            }

            if (!_deferPhysicalCommit)
            {
                await _transaction.CommitAsync(cancellationToken);
                _isPhysicallyCommitted = true;
            }

            _isCommitRequested = true;
        }

        internal async Task CommitPhysicalAsync(
            CancellationToken cancellationToken)
        {
            if (_isRolledBack)
            {
                throw new InvalidOperationException(
                    "Cannot physically commit a transaction that has already been rolled back.");
            }

            if (_isPhysicallyCommitted)
                return;

            if (_deferPhysicalCommit)
            {
                await _transaction.CommitAsync(cancellationToken);
            }

            _isPhysicallyCommitted = true;
            _isCommitRequested = true;
        }

        public async Task RollbackAsync(
            CancellationToken cancellationToken = default)
        {
            if (IsCompleted)
                return;

            await RollbackPhysicalAsync(cancellationToken);
        }

        internal async Task RollbackPhysicalAsync(
            CancellationToken cancellationToken)
        {
            if (_isRolledBack || _isPhysicallyCommitted)
                return;

            try
            {
                await _transaction.RollbackAsync(cancellationToken);
                _isRolledBack = true;
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                if (!_isPhysicallyCommitted)
                {
                    _context.ChangeTracker.Clear();
                }
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }
    }
}
