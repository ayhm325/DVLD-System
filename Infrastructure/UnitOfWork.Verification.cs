using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure;

public sealed partial class UnitOfWork
{
    private async Task<bool> VerifyCommitOutcomeAsync(
        Func<CancellationToken, Task<bool>>? verifySucceeded,
        TransactionVerificationTracker tracker,
        Exception commitException)
    {
        if (verifySucceeded is null && !tracker.CanVerify)
        {
            throw new InvalidOperationException(
                "Transaction commit failed with an ambiguous outcome and no verification probe is available to safely retry.",
                commitException);
        }

        try
        {
            var verificationStrategy = _context.Database.CreateExecutionStrategy();

            return await verificationStrategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                if (verifySucceeded is not null)
                {
                    return await verifySucceeded(CancellationToken.None);
                }

                return await tracker.VerifyCommittedAsync(
                    _context,
                    CancellationToken.None);
            });
        }
        catch (Exception verificationException)
        {
            throw new InvalidOperationException(
                "Transaction commit failed and the transaction outcome could not be verified because the database is unavailable.",
                new AggregateException(commitException, verificationException));
        }
    }

    private sealed class TransactionVerificationTracker : IDisposable
    {
        private readonly DVLDDbContext _context;
        private readonly List<(EntityEntry Entry, IReadOnlyList<IProperty> KeyProperties)> _stagedAddedEntries = [];
        private readonly List<StagedModifiedProbe> _stagedModifiedProbes = [];
        private readonly List<EntityProbeKey> _stagedDeletedProbes = [];

        private readonly HashSet<EntityProbeKey> _addedProbes = [];
        private readonly Dictionary<EntityProbeKey, Dictionary<string, object?>> _modifiedProbes = [];
        private readonly HashSet<EntityProbeKey> _deletedProbes = [];

        private bool _detached;

        public TransactionVerificationTracker(DVLDDbContext context)
        {
            _context = context;
            _context.SavingChanges += OnSavingChanges;
            _context.SavedChanges += OnSavedChanges;
        }

        public bool CanVerify =>
            _addedProbes.Count > 0 ||
            _modifiedProbes.Count > 0 ||
            _deletedProbes.Count > 0;

        public void Detach()
        {
            if (_detached)
                return;

            _detached = true;
            _context.SavingChanges -= OnSavingChanges;
            _context.SavedChanges -= OnSavedChanges;
            ClearStaged();
        }

        public void Dispose() => Detach();

        public async Task<bool> VerifyCommittedAsync(
            DVLDDbContext context,
            CancellationToken cancellationToken)
        {
            foreach (var added in _addedProbes)
            {
                var entity = await context.FindAsync(
                    added.EntityType,
                    added.KeyValues,
                    cancellationToken);

                context.ChangeTracker.Clear();

                if (entity is null)
                    return false;
            }

            if (_addedProbes.Count == 0)
            {
                foreach (var (probeKey, expectedProperties) in _modifiedProbes)
                {
                    var entity = await context.FindAsync(
                        probeKey.EntityType,
                        probeKey.KeyValues,
                        cancellationToken);

                    if (entity is null)
                    {
                        context.ChangeTracker.Clear();
                        return false;
                    }

                    var entry = context.Entry(entity);

                    foreach (var (propName, expectedValue) in expectedProperties)
                    {
                        var actualValue = entry.Property(propName).CurrentValue;
                        if (!ValuesMatch(actualValue, expectedValue))
                        {
                            context.ChangeTracker.Clear();
                            return false;
                        }
                    }

                    context.ChangeTracker.Clear();
                }

                foreach (var deleted in _deletedProbes)
                {
                    var entity = await context.FindAsync(
                        deleted.EntityType,
                        deleted.KeyValues,
                        cancellationToken);

                    context.ChangeTracker.Clear();

                    if (entity is not null)
                        return false;
                }
            }

            return true;
        }

        private void OnSavingChanges(object? sender, SavingChangesEventArgs e)
        {
            ClearStaged();

            foreach (var entry in _context.ChangeTracker.Entries())
            {
                var primaryKey = entry.Metadata.FindPrimaryKey();
                if (primaryKey is null)
                    continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        _stagedAddedEntries.Add((entry, primaryKey.Properties));
                        break;

                    case EntityState.Modified:
                    {
                        var keyValues = ExtractKeyValues(entry, primaryKey.Properties);
                        var modifiedProps = entry.Properties
                            .Where(p =>
                                p.IsModified &&
                                !p.Metadata.IsPrimaryKey() &&
                                !ValuesMatch(p.OriginalValue, p.CurrentValue))
                            .Select(p => (p.Metadata.Name, p.CurrentValue))
                            .ToList();

                        if (modifiedProps.Count > 0)
                        {
                            _stagedModifiedProbes.Add(
                                new StagedModifiedProbe(
                                    entry.Metadata.ClrType,
                                    keyValues,
                                    modifiedProps));
                        }

                        break;
                    }

                    case EntityState.Deleted:
                    {
                        var keyValues = ExtractKeyValues(entry, primaryKey.Properties);
                        _stagedDeletedProbes.Add(
                            new EntityProbeKey(entry.Metadata.ClrType, keyValues));
                        break;
                    }
                }
            }
        }

        private void OnSavedChanges(object? sender, SavedChangesEventArgs e)
        {
            if (e.EntitiesSavedCount <= 0)
            {
                ClearStaged();
                return;
            }

            foreach (var (entry, keyProperties) in _stagedAddedEntries)
            {
                var keyValues = ExtractKeyValues(entry, keyProperties);
                var probeKey = new EntityProbeKey(entry.Metadata.ClrType, keyValues);
                _deletedProbes.Remove(probeKey);
                _addedProbes.Add(probeKey);
            }

            foreach (var modified in _stagedModifiedProbes)
            {
                var probeKey = new EntityProbeKey(modified.EntityType, modified.KeyValues);
                if (_addedProbes.Contains(probeKey))
                    continue;

                if (!_modifiedProbes.TryGetValue(probeKey, out var propMap))
                {
                    propMap = new Dictionary<string, object?>(StringComparer.Ordinal);
                    _modifiedProbes[probeKey] = propMap;
                }

                foreach (var (propName, currentValue) in modified.ModifiedProperties)
                {
                    propMap[propName] = currentValue;
                }
            }

            foreach (var deletedKey in _stagedDeletedProbes)
            {
                _addedProbes.Remove(deletedKey);
                _modifiedProbes.Remove(deletedKey);
                _deletedProbes.Add(deletedKey);
            }

            ClearStaged();
        }

        private void ClearStaged()
        {
            _stagedAddedEntries.Clear();
            _stagedModifiedProbes.Clear();
            _stagedDeletedProbes.Clear();
        }

        private static object?[] ExtractKeyValues(
            EntityEntry entry,
            IReadOnlyList<IProperty> keyProperties)
        {
            var values = new object?[keyProperties.Count];
            for (var i = 0; i < keyProperties.Count; i++)
            {
                values[i] = entry.Property(keyProperties[i].Name).CurrentValue;
            }

            return values;
        }

        private static bool ValuesMatch(object? actual, object? expected)
        {
            if (actual is DateTime actualDate && expected is DateTime expectedDate)
            {
                return Math.Abs((actualDate - expectedDate).TotalMilliseconds) < 10;
            }

            return Equals(actual, expected);
        }
    }

    private sealed record StagedModifiedProbe(
        Type EntityType,
        object?[] KeyValues,
        List<(string PropertyName, object? CurrentValue)> ModifiedProperties);

    private readonly struct EntityProbeKey : IEquatable<EntityProbeKey>
    {
        public EntityProbeKey(Type entityType, object?[] keyValues)
        {
            EntityType = entityType;
            KeyValues = keyValues;
        }

        public Type EntityType { get; }

        public object?[] KeyValues { get; }

        public bool Equals(EntityProbeKey other)
        {
            if (EntityType != other.EntityType ||
                KeyValues.Length != other.KeyValues.Length)
            {
                return false;
            }

            for (var i = 0; i < KeyValues.Length; i++)
            {
                if (!Equals(KeyValues[i], other.KeyValues[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) =>
            obj is EntityProbeKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(EntityType);
            foreach (var key in KeyValues)
            {
                hash.Add(key);
            }

            return hash.ToHashCode();
        }
    }
}
