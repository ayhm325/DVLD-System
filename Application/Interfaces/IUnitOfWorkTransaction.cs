namespace Application.Interfaces;

public interface IUnitOfWorkTransaction
    : IAsyncDisposable
{
    bool IsCommitRequested { get; }

    bool IsPhysicallyCommitted { get; }

    bool IsRolledBack { get; }

    bool IsCompleted { get; }

    Task CommitAsync(
        CancellationToken cancellationToken = default);

    Task RollbackAsync(
        CancellationToken cancellationToken = default);
}