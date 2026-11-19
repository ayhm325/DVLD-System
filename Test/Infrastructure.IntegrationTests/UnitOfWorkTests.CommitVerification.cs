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
    public async Task ExecuteInTransactionAsync_WhenConnectionDropsAfterServerCommit_ShouldVerifyCommitAndReturnSuccessWithoutRetry()
    {
        var className = $"PostCommitDrop_{Guid.NewGuid():N}";
        var interceptor = new FaultInjectingTransactionInterceptor(
            failAfterServerCommitOnAttempt: 1);
        var operationAttempts = 0;

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(
                _database.ConnectionString,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromMilliseconds(20),
                    errorNumbersToAdd: null))
            .AddInterceptors(interceptor)
            .Options;

        int returnedLicenseClassId;

        await using (var context = new DVLDDbContext(options))
        {
            var unitOfWork = new UnitOfWork(context);

            var result = await unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    Interlocked.Increment(ref operationAttempts);

                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Post-commit ambiguous failure test",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 150
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Result<int>.Success(licenseClass.LicenseClassID);
                },
                IsolationLevel.Serializable);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value > 0);
            Assert.Equal(1, operationAttempts);
            Assert.Empty(context.ChangeTracker.Entries());
            returnedLicenseClassId = result.Value;
        }

        await using var verificationContext = _database.CreateContext();
        var persisted = await verificationContext.LicenseClasses
            .AsNoTracking()
            .Where(x => x.ClassName == className)
            .ToListAsync();

        Assert.Single(persisted);
        Assert.Equal(returnedLicenseClassId, persisted[0].LicenseClassID);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenCustomVerifySucceededCallbackConfirmsCommit_ShouldReturnResultWithoutRetry()
    {
        var className = $"CustomVerifyCommit_{Guid.NewGuid():N}";
        var interceptor = new FaultInjectingTransactionInterceptor(
            failAfterServerCommitOnAttempt: 1);
        var operationAttempts = 0;
        var verificationCalls = 0;

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(
                _database.ConnectionString,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromMilliseconds(20),
                    errorNumbersToAdd: null))
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new DVLDDbContext(options);
        var unitOfWork = new UnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                Interlocked.Increment(ref operationAttempts);

                var licenseClass = new LicenseClass
                {
                    ClassName = className,
                    ClassDescription = "Custom verification callback test",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 120
                };

                context.LicenseClasses.Add(licenseClass);
                await unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return Result<int>.Success(licenseClass.LicenseClassID);
            },
            verifySucceeded: async ct =>
            {
                Interlocked.Increment(ref verificationCalls);
                return await context.LicenseClasses
                    .AsNoTracking()
                    .AnyAsync(x => x.ClassName == className, ct);
            },
            IsolationLevel.Serializable);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operationAttempts);
        Assert.Equal(1, verificationCalls);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenTransientFailureOccursBeforeServerCommitsDuringCommitCall_ShouldVerifyNotCommittedAndRetrySafely()
    {
        var className = $"RollbackOnCommitAttempt_{Guid.NewGuid():N}";
        var interceptor = new FaultInjectingTransactionInterceptor(
            rollbackAndFailBeforeServerCommitOnAttempt: 1);
        var operationAttempts = 0;

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(
                _database.ConnectionString,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromMilliseconds(20),
                    errorNumbersToAdd: null))
            .AddInterceptors(interceptor)
            .Options;

        int returnedLicenseClassId;

        await using (var context = new DVLDDbContext(options))
        {
            var unitOfWork = new UnitOfWork(context);

            var result = await unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    Interlocked.Increment(ref operationAttempts);
                    Assert.Empty(context.ChangeTracker.Entries());

                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Commit rolled back on attempt 1",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Result<int>.Success(licenseClass.LicenseClassID);
                },
                IsolationLevel.Serializable);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, operationAttempts);
            returnedLicenseClassId = result.Value;
        }

        await using var verificationContext = _database.CreateContext();
        var persisted = await verificationContext.LicenseClasses
            .AsNoTracking()
            .Where(x => x.ClassName == className)
            .ToListAsync();

        Assert.Single(persisted);
        Assert.Equal(returnedLicenseClassId, persisted[0].LicenseClassID);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenCommitAndVerificationBothFail_ShouldThrowInvalidOperationExceptionWithoutRepeatingOperation()
    {
        var className = $"UnverifiableCommit_{Guid.NewGuid():N}";
        var interceptor = new FaultInjectingTransactionInterceptor(
            failAfterServerCommitOnAttempt: 1);
        var operationAttempts = 0;

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(
                _database.ConnectionString,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 2,
                    maxRetryDelay: TimeSpan.FromMilliseconds(10),
                    errorNumbersToAdd: null))
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new DVLDDbContext(options);
        var unitOfWork = new UnitOfWork(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    Interlocked.Increment(ref operationAttempts);

                    var licenseClass = new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription = "Verification failure test",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    };

                    context.LicenseClasses.Add(licenseClass);
                    await unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Result<int>.Success(licenseClass.LicenseClassID);
                },
                verifySucceeded: _ => throw new TimeoutException("Database unavailable during verification."),
                IsolationLevel.Serializable));

        Assert.Contains("could not be verified", exception.Message);
        Assert.Equal(1, operationAttempts);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenModifiedEntityCommitConfirmationIsLost_ShouldVerifyUpdatedPropertyAndReturnSuccessWithoutRetry()
    {
        var initialName = $"UpdateVerifyInitial_{Guid.NewGuid():N}";
        var updatedDescription = $"UpdatedDesc_{Guid.NewGuid():N}";
        int licenseClassId;

        await using (var seedContext = _database.CreateContext())
        {
            var entity = new LicenseClass
            {
                ClassName = initialName,
                ClassDescription = "Initial description",
                MinimumAllowedAge = 18,
                DefaultValidityLength = 10,
                ClassFees = 100
            };

            seedContext.LicenseClasses.Add(entity);
            await seedContext.SaveChangesAsync();
            licenseClassId = entity.LicenseClassID;
        }

        var interceptor = new FaultInjectingTransactionInterceptor(
            failAfterServerCommitOnAttempt: 1);
        var operationAttempts = 0;

        var options = new DbContextOptionsBuilder<DVLDDbContext>()
            .UseSqlServer(
                _database.ConnectionString,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromMilliseconds(20),
                    errorNumbersToAdd: null))
            .AddInterceptors(interceptor)
            .Options;

        await using (var context = new DVLDDbContext(options))
        {
            var unitOfWork = new UnitOfWork(context);

            var result = await unitOfWork.ExecuteInTransactionAsync(
                async transaction =>
                {
                    Interlocked.Increment(ref operationAttempts);

                    var entity = await context.LicenseClasses
                        .FirstAsync(x => x.LicenseClassID == licenseClassId);

                    entity.ClassDescription = updatedDescription;
                    await unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Result.Success();
                },
                IsolationLevel.Serializable);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, operationAttempts);
            Assert.Empty(context.ChangeTracker.Entries());
        }

        await using var verificationContext = _database.CreateContext();
        var updatedEntity = await verificationContext.LicenseClasses
            .AsNoTracking()
            .SingleAsync(x => x.LicenseClassID == licenseClassId);

        Assert.Equal(updatedDescription, updatedEntity.ClassDescription);
    }

}
