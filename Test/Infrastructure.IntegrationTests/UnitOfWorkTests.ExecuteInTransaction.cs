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
    [Fact]
    public async Task ExecuteInTransactionAsync_ShouldPersistEntity()
    {
        // Arrange
        const string className =
            "Execute Transaction Integration Test Class";

        await using (var context = _database.CreateContext())
        {
            var unitOfWork =
                new UnitOfWork(context);

            // Act
            var result =
                await unitOfWork.ExecuteInTransactionAsync(
                    async transaction =>
                    {
                        var licenseClass =
                            new LicenseClass
                            {
                                ClassName = className,
                                ClassDescription =
                                    "Execute transaction integration test class",
                                MinimumAllowedAge = 18,
                                DefaultValidityLength = 10,
                                ClassFees = 100
                            };

                        context.LicenseClasses.Add(licenseClass);

                        await unitOfWork.SaveChangesAsync();

                        await transaction.CommitAsync();

                        var exists =
                            await context.LicenseClasses
                                .AnyAsync(
                                    x => x.ClassName == className);

                        Assert.True(exists);

                        return true;
                    },
                    IsolationLevel.Serializable);

            // Assert
            Assert.True(result);
        }

        await using var verificationContext =
            _database.CreateContext();

        var persistedLicenseClass =
            await verificationContext.LicenseClasses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.ClassName == className);

        Assert.NotNull(persistedLicenseClass);
        Assert.Equal(
            className,
            persistedLicenseClass.ClassName);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenOperationReturnsFailureResult_ShouldRollbackAndClearChangeTracker()
    {
        // Arrange
        var failedClassName =
            $"Failed Result Class {Guid.NewGuid():N}";
        var subsequentClassName =
            $"Subsequent Result Class {Guid.NewGuid():N}";

        await using (var context = _database.CreateContext())
        {
            var unitOfWork = new UnitOfWork(context);

            var result =
                await unitOfWork.ExecuteInTransactionAsync(
                    async _ =>
                    {
                        context.LicenseClasses.Add(
                            new LicenseClass
                            {
                                ClassName = failedClassName,
                                ClassDescription = "First step saved before failure",
                                MinimumAllowedAge = 18,
                                DefaultValidityLength = 10,
                                ClassFees = 100
                            });

                        await unitOfWork.SaveChangesAsync();

                        return Result.Failure("Second step failed.");
                    },
                    IsolationLevel.Serializable);

            Assert.True(result.IsFailure);
            Assert.Empty(context.ChangeTracker.Entries());

            // Subsequent operation on the same UnitOfWork must work cleanly and not save failed changes.
            context.LicenseClasses.Add(
                new LicenseClass
                {
                    ClassName = subsequentClassName,
                    ClassDescription = "Subsequent valid operation",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                });

            await unitOfWork.SaveChangesAsync();
        }

        await using var verificationContext = _database.CreateContext();

        Assert.False(
            await verificationContext.LicenseClasses
                .AsNoTracking()
                .AnyAsync(x => x.ClassName == failedClassName));

        Assert.True(
            await verificationContext.LicenseClasses
                .AsNoTracking()
                .AnyAsync(x => x.ClassName == subsequentClassName));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenExceptionOccursAfterFirstSave_ShouldRollbackAllChangesAndClearChangeTracker()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString("N");
        var countryName = $"TxExceptionCountry_{uniqueId}";

        await using (var context = _database.CreateContext())
        {
            var unitOfWork = new UnitOfWork(context);
            var countryRepo = new CountryRepository(context);
            var personRepo = new PersonRepository(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync<Result>(
                    async _ =>
                    {
                        var country = new Country
                        {
                            CountryName = countryName
                        };

                        context.Countries.Add(country);
                        await unitOfWork.SaveChangesAsync();

                        await personRepo.AddPersonAsync(
                            new Person
                            {
                                NationalNo = $"EX{uniqueId[..18]}",
                                FirstName = "Tx",
                                SecondName = "Exception",
                                LastName = "Test",
                                DateOfBirth = new DateTime(1990, 1, 1),
                                Gender = Gender.Male,
                                Address = "Test Address",
                                Phone = $"07{uniqueId[..8]}",
                                Email = $"{uniqueId}@tx.test",
                                NationalityCountryID = country.CountryId
                            });

                        throw new InvalidOperationException("Simulated step 2 failure.");
                    },
                    IsolationLevel.Serializable));

            Assert.Empty(context.ChangeTracker.Entries());
        }

        await using var verificationContext = _database.CreateContext();

        Assert.False(
            await verificationContext.Countries
                .AsNoTracking()
                .AnyAsync(c => c.CountryName == countryName));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenOperationCanceledAfterFirstSave_ShouldRollbackWithCancellationTokenNoneAndRethrow()
    {
        var uniqueId = Guid.NewGuid().ToString("N");
        var countryName = $"TxCanceledCountry_{uniqueId}";
        using var cts = new CancellationTokenSource();

        await using (var context = _database.CreateContext())
        {
            var unitOfWork = new UnitOfWork(context);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                unitOfWork.ExecuteInTransactionAsync<Result>(
                    async _ =>
                    {
                        context.Countries.Add(new Country { CountryName = countryName });
                        await unitOfWork.SaveChangesAsync(cts.Token);

                        cts.Cancel();
                        cts.Token.ThrowIfCancellationRequested();

                        return Result.Success();
                    },
                    IsolationLevel.Serializable,
                    cts.Token));

            Assert.Empty(context.ChangeTracker.Entries());
        }

        await using var verificationContext = _database.CreateContext();

        Assert.False(
            await verificationContext.Countries
                .AsNoTracking()
                .AnyAsync(c => c.CountryName == countryName));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenTransactionAlreadyActive_ShouldThrowInvalidOperationException()
    {
        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        await using var outerTransaction =
            await unitOfWork.BeginTransactionAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                _ => Task.FromResult(Result.Success()),
                IsolationLevel.Serializable));

        Assert.Contains("Nested transactions are not supported", exception.Message);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenTransientFailureOccursBeforeCommit_ShouldRetryAndPersistSingleEntity()
    {
        var className = $"PreCommitTransientRetry_{Guid.NewGuid():N}";
        var attemptCount = 0;

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(
                _database.ConnectionString,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromMilliseconds(20),
                    errorNumbersToAdd: null))
            .Options;

        await using (var context = new DVLDDbContext(options))
        {
            var unitOfWork = new UnitOfWork(context);

            var result = await unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    var currentAttempt = Interlocked.Increment(ref attemptCount);

                    // Ensure ChangeTracker was cleared after the failed first attempt.
                    Assert.Empty(context.ChangeTracker.Entries());

                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Pre-commit transient failure test",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();

                    if (currentAttempt == 1)
                    {
                        throw new TimeoutException("Simulated transient failure before commit.");
                    }

                    await transaction.CommitAsync();
                    return Result<int>.Success(licenseClass.LicenseClassID);
                },
                IsolationLevel.Serializable);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value > 0);
            Assert.Equal(2, attemptCount);
        }

        await using var verificationContext = _database.CreateContext();
        var persisted = await verificationContext.LicenseClasses
            .AsNoTracking()
            .Where(x => x.ClassName == className)
            .ToListAsync();

        Assert.Single(persisted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Case1_WhenFailureReturnedWithoutExplicitCommit_ShouldRollbackAndClearChangeTracker()
    {
        var className = $"Case1_RollbackOnFailure_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Case 1 test class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);
                await unitOfWork.SaveChangesAsync();

                Assert.False(transaction.IsCommitRequested);
                Assert.False(transaction.IsPhysicallyCommitted);
                Assert.False(transaction.IsRolledBack);
                Assert.False(transaction.IsCompleted);

                return Result<int>.FromFailure("Validation failed before commit.");
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation failed before commit.", result.Error);
        Assert.Empty(context.ChangeTracker.Entries());

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.Null(persisted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Case2_WhenExplicitCommitCalledThenUnauthorizedReturned_ShouldPhysicallyCommitAndReturnFailure()
    {
        var className = $"Case2_CommitThenUnauthorized_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Case 2 test class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);
                await unitOfWork.SaveChangesAsync();

                await transaction.CommitAsync();

                Assert.True(transaction.IsCommitRequested);
                Assert.False(transaction.IsPhysicallyCommitted);
                Assert.False(transaction.IsRolledBack);
                Assert.False(transaction.IsCompleted);

                return Result<int>.FromUnauthorized("Session revoked due to reuse.");
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        Assert.Equal("Session revoked due to reuse.", result.Error);

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.NotNull(persisted);
        Assert.Equal(className, persisted.ClassName);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Case3_WhenExplicitCommitCalledThenConflictReturned_ShouldPhysicallyCommitAndReturnConflict()
    {
        var className = $"Case3_CommitThenConflict_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Case 3 test class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);
                await unitOfWork.SaveChangesAsync();

                await transaction.CommitAsync();

                Assert.True(transaction.IsCommitRequested);
                Assert.False(transaction.IsPhysicallyCommitted);
                Assert.False(transaction.IsRolledBack);
                Assert.False(transaction.IsCompleted);

                return Result<int>.FromConflict("Audit log saved, state conflict reported.");
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Audit log saved, state conflict reported.", result.Error);

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.NotNull(persisted);
        Assert.Equal(className, persisted.ClassName);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Case4_WhenExceptionThrown_ShouldRollbackAndClearChangeTracker()
    {
        var className = $"Case4_ExceptionRollback_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync<int>(
                async transaction =>
                {
                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Case 4 test class",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();

                    throw new InvalidOperationException("Simulated unexpected exception.");
                },
                IsolationLevel.ReadCommitted));

        Assert.Equal("Simulated unexpected exception.", exception.Message);
        Assert.Empty(context.ChangeTracker.Entries());

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.Null(persisted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Case5_WhenSuccessReturnedWithoutExplicitCommit_ShouldAutoCommitAndPersist()
    {
        var className = $"Case5_AutoCommitSuccess_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Case 5 test class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);
                await unitOfWork.SaveChangesAsync();

                Assert.False(transaction.IsCommitRequested);
                Assert.False(transaction.IsPhysicallyCommitted);
                Assert.False(transaction.IsRolledBack);
                Assert.False(transaction.IsCompleted);

                return Result<int>.Success(licenseClass.LicenseClassID);
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value > 0);

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.NotNull(persisted);
        Assert.Equal(result.Value, persisted.LicenseClassID);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenExplicitRollbackCalled_ShouldRollbackEvenIfOperationReturnsSuccess()
    {
        var className = $"RollbackWithSuccess_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Rollback explicit test class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);
                await unitOfWork.SaveChangesAsync();

                await transaction.RollbackAsync();

                Assert.False(transaction.IsCommitRequested);
                Assert.False(transaction.IsPhysicallyCommitted);
                Assert.True(transaction.IsRolledBack);
                Assert.True(transaction.IsCompleted);

                return Result<int>.Success(999);
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.Value);
        Assert.Empty(context.ChangeTracker.Entries());

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.Null(persisted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenEntityMutatedAfterCommitAsync_ShouldPersistSubsequentMutations()
    {
        var className = $"PostCommitMutation_{Guid.NewGuid():N}";
        const string initialDescription = "Initial description before CommitAsync";
        const string updatedDescription = "Updated description after CommitAsync";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = initialDescription,
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);
                await transaction.CommitAsync();

                Assert.True(transaction.IsCommitRequested);
                Assert.False(transaction.IsPhysicallyCommitted);

                // Mutate the entity after CommitAsync was requested
                licenseClass.ClassDescription = updatedDescription;

                return Result<int>.Success(licenseClass.LicenseClassID);
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsSuccess);

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.NotNull(persisted);
        Assert.Equal(updatedDescription, persisted.ClassDescription);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenCommitAsyncCalledRepeatedly_ShouldBeIdempotent()
    {
        var className = $"RepeatedCommit_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Repeated commit test class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);

                await transaction.CommitAsync();
                Assert.True(transaction.IsCommitRequested);

                // Second commit call should be a no-op and not throw
                await transaction.CommitAsync();
                Assert.True(transaction.IsCommitRequested);

                return Result<int>.Success(licenseClass.LicenseClassID);
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsSuccess);

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenCommitAsyncCalledAfterRollback_ShouldThrowInvalidOperationException()
    {
        var className = $"CommitAfterRollback_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Commit after rollback test class",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();

                    await transaction.RollbackAsync();

                    // Attempting CommitAsync after RollbackAsync must throw
                    await transaction.CommitAsync();

                    return Result.Success();
                },
                IsolationLevel.ReadCommitted);
        });

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.Null(persisted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenRollbackAsyncCalledAfterPhysicalCommit_ShouldBeNoOp()
    {
        var className = $"RollbackAfterPhysicalCommit_{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        IUnitOfWorkTransaction? capturedTransaction = null;

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                capturedTransaction = transaction;

                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Rollback after physical commit test class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

                context.LicenseClasses.Add(licenseClass);
                await unitOfWork.SaveChangesAsync();
                return Result<int>.Success(licenseClass.LicenseClassID);
            },
            IsolationLevel.ReadCommitted);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedTransaction);
        Assert.True(capturedTransaction.IsPhysicallyCommitted);
        Assert.True(capturedTransaction.IsCompleted);

        // Calling RollbackAsync after transaction is physically committed must be a safe no-op
        await capturedTransaction.RollbackAsync();
        Assert.False(capturedTransaction.IsRolledBack);

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.LicenseClasses
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClassName == className);

        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenRollbackFails_ShouldNotClaimIsRolledBackAndShouldPropagateException()
    {
        var className = $"RollbackFail_{Guid.NewGuid():N}";
        var interceptor = new FaultInjectingTransactionInterceptor(failOnRollback: true);

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new DVLDDbContext(options);
        var unitOfWork = new UnitOfWork(context);

        IUnitOfWorkTransaction? capturedTx = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    capturedTx = transaction;

                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Rollback failure test class",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();

                    // Explicitly call RollbackAsync, which triggers the interceptor failure
                    await transaction.RollbackAsync();

                    return Result.Success();
                },
                IsolationLevel.ReadCommitted);
        });

        Assert.Contains("Simulated physical rollback failure", ex.Message);
        Assert.NotNull(capturedTx);
        // INVARIANT: When physical rollback fails, IsRolledBack must NOT be true!
        Assert.False(capturedTx.IsRolledBack, "IsRolledBack must not claim true when the physical rollback failed.");
        Assert.False(capturedTx.IsPhysicallyCommitted);
        Assert.False(capturedTx.IsCompleted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenOperationThrowsAndRollbackFails_ShouldPreserveOriginalException()
    {
        var className = $"OpThrowRollbackFail_{Guid.NewGuid():N}";
        var interceptor = new FaultInjectingTransactionInterceptor(failOnRollback: true);

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new DVLDDbContext(options);
        var unitOfWork = new UnitOfWork(context);

        IUnitOfWorkTransaction? capturedTx = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await unitOfWork.ExecuteInTransactionAsync<int>(
                async transaction =>
                {
                    capturedTx = transaction;

                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Op throw and rollback fail test class",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();

                    throw new InvalidOperationException("Primary business operation failure.");
                },
                IsolationLevel.ReadCommitted);
        });

        // The primary exception is preserved and thrown
        Assert.Equal("Primary business operation failure.", ex.Message);
        Assert.True(ex.Data.Contains("RollbackException"));
        Assert.Contains("Simulated physical rollback failure", ex.Data["RollbackException"]!.ToString());

        Assert.NotNull(capturedTx);
        Assert.False(capturedTx.IsRolledBack, "IsRolledBack must not claim success when rollback failed.");
        Assert.False(capturedTx.IsPhysicallyCommitted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenResultFailureReturnedAndRollbackFails_ShouldThrowAndNotReportSuccess()
    {
        var className = $"ResultFailRollbackFail_{Guid.NewGuid():N}";
        var interceptor = new FaultInjectingTransactionInterceptor(failOnRollback: true);

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new DVLDDbContext(options);
        var unitOfWork = new UnitOfWork(context);

        IUnitOfWorkTransaction? capturedTx = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    capturedTx = transaction;

                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Result failure test class",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();

                    return Result.Failure("Domain validation failure.");
                },
                IsolationLevel.ReadCommitted);
        });

        Assert.Contains("Simulated physical rollback failure", ex.Message);
        Assert.NotNull(capturedTx);
        Assert.False(capturedTx.IsRolledBack);
        Assert.False(capturedTx.IsPhysicallyCommitted);
    }

}
