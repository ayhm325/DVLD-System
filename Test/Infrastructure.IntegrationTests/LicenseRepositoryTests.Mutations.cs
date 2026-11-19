using Application.Common.Pagination;
using Application.DTOs.LicenseDTO;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public sealed partial class LicenseRepositoryTests
{
    [Fact]
    public async Task AddLicenseAsync_ShouldPersistLicenseAfterSaveChanges()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync();

        var application =
            await SeedAdditionalApplicationAsync(
                seed.PersonId,
                seed.UserId);

        var license =
            new License
            {
                ApplicationID =
                    application.ApplicationId,
                DriverID =
                    seed.DriverId,
                LicenseClass =
                    seed.LicenseClassId,
                IssueDate =
                    DateTime.UtcNow,
                ExpirationDate =
                    DateTime.UtcNow.AddYears(1),
                Notes =
                    "AddLicenseAsync integration test",
                PaidFees = 25,
                IsActive = false,
                IssueReason =
                    IssueReason.Renew,
                CreatedByUserID =
                    seed.UserId
            };

        await using (var context = _database.CreateContext())
        {
            var repository =
                new LicenseRepository(context);

            // Act
            await repository.AddLicenseAsync(license);

            await context.SaveChangesAsync();
        }

        // Assert
        await using var verificationContext =
            _database.CreateContext();

        var persisted =
            await verificationContext.Licenses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.LicenseID == license.LicenseID);

        Assert.NotNull(persisted);
        Assert.Equal(
            application.ApplicationId,
            persisted.ApplicationID);
        Assert.Equal(
            seed.DriverId,
            persisted.DriverID);
        Assert.Equal(
            seed.LicenseClassId,
            persisted.LicenseClass);
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task AddLicenseAsync_ShouldThrowForNullLicense()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () =>
                repository.AddLicenseAsync(null!));
    }

    [Fact]
    public async Task DeactivateLicenseAsync_ShouldDeactivateActiveLicense()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: true);

        await using (var context = _database.CreateContext())
        {
            var repository =
                new LicenseRepository(context);

            // Act
            var result =
                await repository.DeactivateLicenseAsync(
                    seed.LicenseId);

            // Assert
            Assert.True(result);
        }

        // Verify against a new DbContext.
        await using var verificationContext =
            _database.CreateContext();

        var license =
            await verificationContext.Licenses
                .AsNoTracking()
                .SingleAsync(
                    x => x.LicenseID == seed.LicenseId);

        Assert.False(license.IsActive);
    }

    [Fact]
    public async Task DeactivateLicenseAsync_ShouldReturnFalseForInactiveLicense()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: false);

        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.DeactivateLicenseAsync(
                seed.LicenseId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeactivateLicenseAsync_ShouldReturnFalseForMissingLicense()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.DeactivateLicenseAsync(
                int.MaxValue);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeactivateLicenseAsync_ShouldReturnFalseForInvalidId(
        int licenseId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.DeactivateLicenseAsync(
                licenseId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasAnotherActiveLicenseAsync_ShouldReturnFalseWhenExcludedLicenseIsTheOnlyActiveLicense()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: true);

        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.HasAnotherActiveLicenseAsync(
                seed.DriverId,
                seed.LicenseClassId,
                seed.LicenseId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasAnotherActiveLicenseAsync_ShouldReturnTrueWhenActiveLicenseExistsAndExcludedIdDoesNotMatch()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: true);

        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.HasAnotherActiveLicenseAsync(
                seed.DriverId,
                seed.LicenseClassId,
                int.MaxValue);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, -1)]
    public async Task HasAnotherActiveLicenseAsync_ShouldReturnFalseForInvalidIds(
        int driverId,
        int licenseClassId,
        int excludedLicenseId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.HasAnotherActiveLicenseAsync(
                driverId,
                licenseClassId,
                excludedLicenseId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ActivateLicenseAsync_ShouldActivateValidUnexpiredInactiveLicense()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: false,
                expirationDate:
                    new DateTime(2027, 11, 4));

        await using (var context = _database.CreateContext())
        {
            var repository =
                new LicenseRepository(context);

            // Act
            var result =
                await repository.ActivateLicenseAsync(
                    seed.LicenseId,
                    new DateOnly(2026, 11, 4));

            // Assert
            Assert.True(result);
        }

        // Verify against a new DbContext.
        await using var verificationContext =
            _database.CreateContext();

        var license =
            await verificationContext.Licenses
                .AsNoTracking()
                .SingleAsync(
                    x => x.LicenseID == seed.LicenseId);

        Assert.True(license.IsActive);
    }

    [Fact]
    public async Task ActivateLicenseAsync_ShouldReturnFalseForExpiredLicense()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: false,
                expirationDate:
                    new DateTime(2026, 11, 3, 23, 59, 59));

        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.ActivateLicenseAsync(
                seed.LicenseId,
                new DateOnly(2026, 11, 4));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ActivateLicenseAsync_ShouldReturnFalseWhenLicenseIsAlreadyActive()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: true,
                expirationDate:
                    new DateTime(2027, 11, 4));

        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.ActivateLicenseAsync(
                seed.LicenseId,
                new DateOnly(2026, 11, 4));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ActivateLicenseAsync_ShouldReturnFalseForMissingLicense()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.ActivateLicenseAsync(
                int.MaxValue,
                new DateOnly(2026, 11, 4));

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ActivateLicenseAsync_ShouldReturnFalseForInvalidId(
        int licenseId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository =
            new LicenseRepository(context);

        // Act
        var result =
            await repository.ActivateLicenseAsync(
                licenseId,
                new DateOnly(2026, 11, 4));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeactivateLicenseAsync_WhenLicenseIsTracked_ShouldSynchronizeTrackedEntityState()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: true);

        await using (var context = _database.CreateContext())
        {
            var trackedLicense =
                await context.Licenses
                    .SingleAsync(x => x.LicenseID == seed.LicenseId);

            Assert.True(trackedLicense.IsActive);

            var repository =
                new LicenseRepository(context);

            // Act
            var result =
                await repository.DeactivateLicenseAsync(
                    seed.LicenseId);

            // Assert
            Assert.True(result);
            Assert.False(trackedLicense.IsActive);
            Assert.False(context.Entry(trackedLicense).Property(x => x.IsActive).IsModified);

            await context.SaveChangesAsync();
        }

        await using var verificationContext =
            _database.CreateContext();

        var persisted =
            await verificationContext.Licenses
                .AsNoTracking()
                .SingleAsync(x => x.LicenseID == seed.LicenseId);

        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task ActivateLicenseAsync_WhenLicenseIsTracked_ShouldSynchronizeTrackedEntityState()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: false,
                expirationDate: new DateTime(2027, 11, 4));

        await using (var context = _database.CreateContext())
        {
            var trackedLicense =
                await context.Licenses
                    .SingleAsync(x => x.LicenseID == seed.LicenseId);

            Assert.False(trackedLicense.IsActive);

            var repository =
                new LicenseRepository(context);

            // Act
            var result =
                await repository.ActivateLicenseAsync(
                    seed.LicenseId,
                    new DateOnly(2026, 11, 4));

            // Assert
            Assert.True(result);
            Assert.True(trackedLicense.IsActive);
            Assert.False(context.Entry(trackedLicense).Property(x => x.IsActive).IsModified);

            await context.SaveChangesAsync();
        }

        await using var verificationContext =
            _database.CreateContext();

        var persisted =
            await verificationContext.Licenses
                .AsNoTracking()
                .SingleAsync(x => x.LicenseID == seed.LicenseId);

        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task ActivateAndDeactivateExpiredLicensesAsync_ShouldHonorOfficialJordanCalendarDateBoundary()
    {
        var sameDaySeed =
            await SeedLicenseAsync(
                isActive: false,
                expirationDate: new DateTime(2026, 11, 4, 0, 0, 0));

        var priorDaySeed =
            await SeedLicenseAsync(
                isActive: true,
                expirationDate: new DateTime(2026, 11, 3, 23, 59, 59));

        await using (var context = _database.CreateContext())
        {
            var repository = new LicenseRepository(context);

            // On 2026-11-04 Jordan, sameDaySeed (expiring 2026-11-04) can still be reactivated
            var activated = await repository.ActivateLicenseAsync(
                sameDaySeed.LicenseId,
                new DateOnly(2026, 11, 4));
            Assert.True(activated);

            // DeactivateExpiredLicensesAsync on 2026-11-04 must keep sameDaySeed active and deactivate priorDaySeed
            var deactivatedCount = await repository.DeactivateExpiredLicensesAsync(
                new DateOnly(2026, 11, 4));
            Assert.True(deactivatedCount >= 1);
        }

        await using var verifyContext = _database.CreateContext();
        var reloadedSameDay = await verifyContext.Licenses.AsNoTracking()
            .SingleAsync(x => x.LicenseID == sameDaySeed.LicenseId);
        var reloadedPriorDay = await verifyContext.Licenses.AsNoTracking()
            .SingleAsync(x => x.LicenseID == priorDaySeed.LicenseId);

        Assert.True(reloadedSameDay.IsActive);
        Assert.False(reloadedPriorDay.IsActive);
    }

}
