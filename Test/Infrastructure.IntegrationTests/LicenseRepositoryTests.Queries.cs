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
    public async Task GetLicenseByIdAsync_ShouldReturnLicenseWithRelatedData()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicenseByIdAsync(seed.LicenseId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(seed.LicenseId, result.LicenseID);

        Assert.NotNull(result.Driver);
        Assert.NotNull(result.Driver.Person);
        Assert.NotNull(result.LicenseClassInfo);
        Assert.NotNull(result.CreatedByUser);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetLicenseByIdAsync_ShouldReturnNullForInvalidId(
        int id)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicenseByIdAsync(id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetForUpdateAsync_ShouldReturnLicenseWithRelatedData()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetForUpdateAsync(seed.LicenseId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(seed.LicenseId, result.LicenseID);

        Assert.NotNull(result.Driver);
        Assert.NotNull(result.Driver.Person);
        Assert.NotNull(result.LicenseClassInfo);
        Assert.NotNull(result.CreatedByUser);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetForUpdateAsync_ShouldReturnNullForInvalidId(
        int id)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetForUpdateAsync(id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLicenseByIdAsync_WhenMultipleReadersExecuteConcurrently_CompleteSuccessfullyWithoutBlocking()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        // Spin up multiple concurrent readers simulating concurrent GET /api/licenses/{id} requests
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var context = _database.CreateContext();
            var repo = new LicenseRepository(context);
            var lic = await repo.GetLicenseByIdAsync(seed.LicenseId);
            Assert.NotNull(lic);
            Assert.Equal(seed.LicenseId, lic.LicenseID);
        });

        // Act & Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task GetByDriverIdAsync_ShouldReturnMostRecentLicense()
    {
        // Arrange
        var older =
            await SeedLicenseAsync(
                issueDate: DateTime.UtcNow.AddYears(-2),
                isActive: false);

        var newer =
            await SeedAdditionalLicenseAsync(
                older,
                issueDate: DateTime.UtcNow.AddYears(-1),
                isActive: true);

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetByDriverIdAsync(
                older.DriverId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(
            newer.LicenseId,
            result.LicenseID);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByDriverIdAsync_ShouldReturnNullForInvalidDriverId(
        int driverId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetByDriverIdAsync(driverId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllLicensesAsync_ShouldReturnLicensesOrderedByIssueDateDescending()
    {
        // Arrange
        var older =
            await SeedLicenseAsync(
                issueDate: DateTime.UtcNow.AddYears(-3));

        var newer =
            await SeedLicenseAsync(
                issueDate: DateTime.UtcNow.AddYears(-1));

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var licenses =
            await repository.GetAllLicensesAsync();

        // Assert
        var newerIndex =
            licenses.FindIndex(
                x => x.LicenseID == newer.LicenseId);

        var olderIndex =
            licenses.FindIndex(
                x => x.LicenseID == older.LicenseId);

        Assert.True(newerIndex >= 0);
        Assert.True(olderIndex >= 0);
        Assert.True(newerIndex < olderIndex);
    }

    [Fact]
    public async Task GetLicensesByDriverIdAsync_ShouldReturnOnlyDriverLicenses()
    {
        // Arrange
        var first =
            await SeedLicenseAsync();

        var second =
            await SeedAdditionalLicenseAsync(
                first,
                issueDate: DateTime.UtcNow.AddDays(1),
                isActive: false);

        var other =
            await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByDriverIdAsync(
                first.DriverId);

        // Assert
        Assert.Contains(
            result,
            x => x.LicenseID == first.LicenseId);

        Assert.Contains(
            result,
            x => x.LicenseID == second.LicenseId);

        Assert.DoesNotContain(
            result,
            x => x.LicenseID == other.LicenseId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetLicensesByDriverIdAsync_ShouldReturnEmptyForInvalidDriverId(
        int driverId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByDriverIdAsync(driverId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLicensesByApplicationIdAsync_ShouldReturnMatchingLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByApplicationIdAsync(
                seed.ApplicationId);

        // Assert
        var license =
            Assert.Single(result);

        Assert.Equal(
            seed.LicenseId,
            license.LicenseID);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetLicensesByApplicationIdAsync_ShouldReturnEmptyForInvalidApplicationId(
        int applicationId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByApplicationIdAsync(
                applicationId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLicensesByLicenseClassIdAsync_ShouldReturnOnlyMatchingClass()
    {
        // Arrange
        var first =
            await SeedLicenseAsync();

        var second =
            await SeedLicenseAsync(
                licenseClassIdOverride: first.LicenseClassId);

        var other =
            await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByLicenseClassIdAsync(
                first.LicenseClassId);

        // Assert
        Assert.Contains(
            result,
            x => x.LicenseID == first.LicenseId);

        Assert.Contains(
            result,
            x => x.LicenseID == second.LicenseId);

        Assert.DoesNotContain(
            result,
            x => x.LicenseID == other.LicenseId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetLicensesByLicenseClassIdAsync_ShouldReturnEmptyForInvalidLicenseClassId(
        int licenseClassId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByLicenseClassIdAsync(
                licenseClassId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLicensesByPersonIdAsync_ShouldReturnOnlyPersonLicenses()
    {
        // Arrange
        var first =
            await SeedLicenseAsync();

        var second =
            await SeedAdditionalLicenseAsync(
                first,
                issueDate: DateTime.UtcNow.AddDays(1),
                isActive: false,
                createNewLicenseClass: true);

        var other =
            await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByPersonIdAsync(
                first.PersonId);

        // Assert
        Assert.Contains(
            result,
            x => x.LicenseID == first.LicenseId);

        Assert.Contains(
            result,
            x => x.LicenseID == second.LicenseId);

        Assert.DoesNotContain(
            result,
            x => x.LicenseID == other.LicenseId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetLicensesByPersonIdAsync_ShouldReturnEmptyForInvalidPersonId(
        int personId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetLicensesByPersonIdAsync(personId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task IsLicenseExistsAsync_ShouldReturnTrueForExistingLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsLicenseExistsAsync(
                seed.LicenseId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsLicenseExistsAsync_ShouldReturnFalseForMissingLicense()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsLicenseExistsAsync(
                int.MaxValue);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IsLicenseExistsAsync_ShouldReturnFalseForInvalidId(
        int id)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsLicenseExistsAsync(id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsDriverHasLicenseAsync_ShouldReturnTrueWhenDriverHasLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsDriverHasLicenseAsync(
                seed.DriverId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsDriverHasLicenseAsync_ShouldReturnFalseWhenDriverHasNoLicense()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsDriverHasLicenseAsync(
                int.MaxValue);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IsDriverHasLicenseAsync_ShouldReturnFalseForInvalidDriverId(
        int driverId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsDriverHasLicenseAsync(driverId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsApplicationHasLicenseAsync_ShouldReturnTrueWhenApplicationHasLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsApplicationHasLicenseAsync(
                seed.ApplicationId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsApplicationHasLicenseAsync_ShouldReturnFalseWhenApplicationHasNoLicense()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsApplicationHasLicenseAsync(
                int.MaxValue);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IsApplicationHasLicenseAsync_ShouldReturnFalseForInvalidApplicationId(
        int applicationId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsApplicationHasLicenseAsync(
                applicationId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsActiveLicenseExistsAsync_ShouldReturnTrueForActiveLicense()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: true);

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsActiveLicenseExistsAsync(
                seed.DriverId,
                seed.LicenseClassId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsActiveLicenseExistsAsync_ShouldReturnFalseForInactiveLicense()
    {
        // Arrange
        var seed =
            await SeedLicenseAsync(
                isActive: false);

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsActiveLicenseExistsAsync(
                seed.DriverId,
                seed.LicenseClassId);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task IsActiveLicenseExistsAsync_ShouldReturnFalseForInvalidIds(
        int driverId,
        int licenseClassId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.IsActiveLicenseExistsAsync(
                driverId,
                licenseClassId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetApplicationIdsWithLicensesAsync_ShouldReturnDistinctMatchingIds()
    {
        // Arrange
        var first =
            await SeedLicenseAsync();

        var second =
            await SeedLicenseAsync();

        var requestedIds = new[]
        {
            first.ApplicationId,
            second.ApplicationId,
            first.ApplicationId,
            0,
            -1,
            int.MaxValue
        };

        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetApplicationIdsWithLicensesAsync(
                requestedIds);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(first.ApplicationId, result);
        Assert.Contains(second.ApplicationId, result);
    }

    [Fact]
    public async Task GetApplicationIdsWithLicensesAsync_ShouldReturnEmptyWhenNoValidIdsExist()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act
        var result =
            await repository.GetApplicationIdsWithLicensesAsync(
                [0, -1, -10]);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetApplicationIdsWithLicensesAsync_ShouldThrowForNullInput()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var repository = new LicenseRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () =>
                repository.GetApplicationIdsWithLicensesAsync(
                    null!));
    }

}
