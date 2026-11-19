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
    : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public UnitOfWorkTests(
        SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task CommitAsync_ShouldPersistEntity()
    {
        // Arrange
        const string className =
            "Commit Integration Test Class";

        await using (var context = _database.CreateContext())
        {
            var unitOfWork =
                new UnitOfWork(context);

            await using var transaction =
                await unitOfWork.BeginTransactionAsync();

            var licenseClass =
                new LicenseClass
                {
                    ClassName = className,
                    ClassDescription =
                        "Commit integration test license class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                };

            // Act
            context.LicenseClasses.Add(licenseClass);

            await unitOfWork.SaveChangesAsync();

            await transaction.CommitAsync();
        }

        // Assert
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
    public async Task RollbackAsync_ShouldNotPersistEntity_AndShouldClearChangeTracker()
    {
        // Arrange
        var className =
            $"Rollback Integration Test Class {Guid.NewGuid():N}";
        var subsequentClassName =
            $"Subsequent Integration Test Class {Guid.NewGuid():N}";

        await using (var context = _database.CreateContext())
        {
            var unitOfWork =
                new UnitOfWork(context);

            await using (var transaction =
                await unitOfWork.BeginTransactionAsync())
            {
                context.LicenseClasses.Add(
                    new LicenseClass
                    {
                        ClassName = className,
                        ClassDescription =
                            "Rollback integration test license class",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    });

                await transaction.RollbackAsync();

                Assert.Empty(context.ChangeTracker.Entries());
            }

            // A subsequent operation on the same UnitOfWork must not persist the rolled-back entity.
            context.LicenseClasses.Add(
                new LicenseClass
                {
                    ClassName = subsequentClassName,
                    ClassDescription =
                        "Subsequent valid license class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                });

            await unitOfWork.SaveChangesAsync();
        }

        // Assert
        await using var verificationContext =
            _database.CreateContext();

        var rolledBackEntity =
            await verificationContext.LicenseClasses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.ClassName == className);

        var subsequentEntity =
            await verificationContext.LicenseClasses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.ClassName == subsequentClassName);

        Assert.Null(rolledBackEntity);
        Assert.NotNull(subsequentEntity);
    }

    [Fact]
    public async Task DisposeAsync_WithoutCommit_ShouldRollbackAndClearChangeTracker()
    {
        // Arrange
        var uncommittedClassName =
            $"Uncommitted Dispose Class {Guid.NewGuid():N}";
        var subsequentClassName =
            $"Subsequent Dispose Class {Guid.NewGuid():N}";

        await using (var context = _database.CreateContext())
        {
            var unitOfWork =
                new UnitOfWork(context);

            await using (var transaction =
                await unitOfWork.BeginTransactionAsync())
            {
                context.LicenseClasses.Add(
                    new LicenseClass
                    {
                        ClassName = uncommittedClassName,
                        ClassDescription = "Uncommitted class",
                        MinimumAllowedAge = 18,
                        DefaultValidityLength = 10,
                        ClassFees = 100
                    });
            }

            Assert.Empty(context.ChangeTracker.Entries());

            context.LicenseClasses.Add(
                new LicenseClass
                {
                    ClassName = subsequentClassName,
                    ClassDescription = "Committed class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 100
                });

            await unitOfWork.SaveChangesAsync();
        }

        await using var verificationContext =
            _database.CreateContext();

        Assert.False(
            await verificationContext.LicenseClasses
                .AsNoTracking()
                .AnyAsync(x => x.ClassName == uncommittedClassName));

        Assert.True(
            await verificationContext.LicenseClasses
                .AsNoTracking()
                .AnyAsync(x => x.ClassName == subsequentClassName));
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenTransactionAlreadyActive_ShouldThrowInvalidOperationException()
    {
        await using var context = _database.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        await using var outerTransaction =
            await unitOfWork.BeginTransactionAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.BeginTransactionAsync());

        Assert.Contains("Nested transactions are not supported", exception.Message);
    }

}
