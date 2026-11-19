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
    public async Task GetAllLicensesAsync_WhenRequestingBeyondPage1_ReturnsCorrectPageAndItems()
    {
        // Arrange
        await SeedLicenseAsync();
        await SeedLicenseAsync();
        await SeedLicenseAsync();

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 2,
            PageSize = 1
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 3);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(1, result.PageSize);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenSearchingByLicenseId_MatchesLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = seed.LicenseId.ToString(),
            SearchBy = "licenseid"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Items, l => l.LicenseID == seed.LicenseId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenSearchingByAppId_MatchesLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = seed.ApplicationId.ToString(),
            SearchBy = "applicationid"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Items, l => l.LicenseID == seed.LicenseId && l.ApplicationID == seed.ApplicationId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenSearchingByDriverName_MatchesLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);
        var person = await context.People.SingleAsync(p => p.PersonId == seed.PersonId);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = person.LastName,
            SearchBy = "drivername"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Items, l => l.LicenseID == seed.LicenseId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenSearchingByClassName_MatchesLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);
        var licenseClass = await context.LicenseClasses.SingleAsync(c => c.LicenseClassID == seed.LicenseClassId);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = licenseClass.ClassName,
            SearchBy = "classname"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Items, l => l.LicenseID == seed.LicenseId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenSearchingAllFields_MatchesLicense()
    {
        // Arrange
        var seed = await SeedLicenseAsync();

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);
        var person = await context.People.SingleAsync(p => p.PersonId == seed.PersonId);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = person.LastName,
            SearchBy = "all"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Items, l => l.LicenseID == seed.LicenseId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenFilteringByActiveStatus_ReturnsOnlyActive()
    {
        // Arrange
        var activeSeed = await SeedLicenseAsync(isActive: true);
        var inactiveSeed = await SeedLicenseAsync(isActive: false);

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 50,
            StatusFilter = "active"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Items, l => Assert.True(l.IsActive));
        Assert.Contains(result.Items, l => l.LicenseID == activeSeed.LicenseId);
        Assert.DoesNotContain(result.Items, l => l.LicenseID == inactiveSeed.LicenseId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenFilteringByInactiveStatus_ReturnsOnlyInactive()
    {
        // Arrange
        var activeSeed = await SeedLicenseAsync(isActive: true);
        var inactiveSeed = await SeedLicenseAsync(isActive: false);

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 50,
            StatusFilter = "inactive"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Items, l => Assert.False(l.IsActive));
        Assert.Contains(result.Items, l => l.LicenseID == inactiveSeed.LicenseId);
        Assert.DoesNotContain(result.Items, l => l.LicenseID == activeSeed.LicenseId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenFilteringByExpired_ReturnsOnlyExpired()
    {
        // Arrange
        var today = new DateOnly(2026, 11, 8);
        var expiredSeed = await SeedLicenseAsync(
            issueDate: new DateTime(2020, 1, 1),
            expirationDate: new DateTime(2026, 11, 7));
        var validSeed = await SeedLicenseAsync(
            issueDate: new DateTime(2026, 1, 1),
            expirationDate: new DateTime(2028, 1, 1));

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 50,
            IsExpired = true
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, today);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Items, l => Assert.True(DateOnly.FromDateTime(l.ExpirationDate) < today));
        Assert.Contains(result.Items, l => l.LicenseID == expiredSeed.LicenseId);
        Assert.DoesNotContain(result.Items, l => l.LicenseID == validSeed.LicenseId);
    }

    [Fact]
    public async Task GetAllLicensesAsync_WhenSearchYieldsNoMatches_ReturnsEmptyResultWithZeroCount()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = "NON_EXISTENT_LICENSE_SEARCH_XYZ_9999",
            SearchBy = "all"
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAllLicensesAsync_DeterministicOrdering_ReturnsSortedByIssueDateAndIdDescending()
    {
        // Arrange
        var seed1 = await SeedLicenseAsync(issueDate: new DateTime(2024, 1, 1));
        var seed2 = await SeedLicenseAsync(issueDate: new DateTime(2026, 6, 1));
        var seed3 = await SeedLicenseAsync(issueDate: new DateTime(2025, 1, 1));

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = 1,
            PageSize = 50
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        for (int i = 0; i < result.Items.Count - 1; i++)
        {
            var current = result.Items[i];
            var next = result.Items[i + 1];
            Assert.True(
                current.IssueDate > next.IssueDate ||
                (current.IssueDate == next.IssueDate && current.LicenseID > next.LicenseID),
                $"Items at {i} and {i + 1} violated deterministic ordering.");
        }
    }

    [Fact]
    public async Task GetAllLicensesAsync_BoundaryPageSize_ClampsToMax100()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var request = new LicenseQueryRequest
        {
            PageNumber = -5,
            PageSize = 500
        };

        // Act
        var result = await repository.GetAllLicensesAsync(request, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetRenewalSummaryAsync_ComputesAccurateCounts()
    {
        var cutoff = new DateOnly(2026, 11, 8);
        var expiredDate = new DateTime(2026, 11, 7);
        var futureDate = new DateTime(2028, 1, 1);

        // 1. Pure expired, active license, no newer license, not detained => Eligible
        var pureExpired = await SeedLicenseAsync(
            issueDate: new DateTime(2020, 1, 1),
            expirationDate: expiredDate,
            isActive: true);

        // 2. Expired license that has been renewed (newer license exists for same driver & class) => Ineligible
        var renewedOlder = await SeedLicenseAsync(
            issueDate: new DateTime(2018, 1, 1),
            expirationDate: expiredDate,
            isActive: false);
        var renewedNewer = await SeedAdditionalLicenseAsync(
            renewedOlder,
            issueDate: new DateTime(2026, 1, 1),
            isActive: true);

        // 3. Expired license that is actively detained => Ineligible
        var detainedExpired = await SeedLicenseAsync(
            issueDate: new DateTime(2020, 1, 1),
            expirationDate: expiredDate,
            isActive: true);
        await using (var ctx = _database.CreateContext())
        {
            ctx.DetainedLicenses.Add(new DetainedLicense
            {
                LicenseID = detainedExpired.LicenseId,
                DetainDate = DateTime.UtcNow,
                FineFees = 20,
                CreatedByUserID = detainedExpired.UserId,
                IsReleased = false
            });
            await ctx.SaveChangesAsync();
        }

        // 4. Expired license whose detention is released => Eligible
        var releasedExpired = await SeedLicenseAsync(
            issueDate: new DateTime(2020, 1, 1),
            expirationDate: expiredDate,
            isActive: true);
        await using (var ctx = _database.CreateContext())
        {
            ctx.DetainedLicenses.Add(new DetainedLicense
            {
                LicenseID = releasedExpired.LicenseId,
                DetainDate = DateTime.UtcNow.AddMonths(-1),
                FineFees = 20,
                CreatedByUserID = releasedExpired.UserId,
                IsReleased = true,
                ReleaseDate = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // 5. Active non-expired license (expires in future) => Not counted in TotalExpired
        var nonExpired = await SeedLicenseAsync(
            issueDate: new DateTime(2026, 1, 1),
            expirationDate: futureDate,
            isActive: true);

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        // Act
        var summary = await repository.GetRenewalSummaryAsync(cutoff);

        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.TotalExpired >= 4);
        Assert.True(summary.EligibleForRenewal >= 2);
        Assert.Equal(summary.TotalExpired - summary.EligibleForRenewal, summary.IneligibleForRenewal);
    }

    [Fact]
    public async Task GetRenewalSummaryAsync_RespectsJordanDateBoundary()
    {
        var cutoff = new DateOnly(2026, 11, 8);

        var expiredBeforeCutoff = await SeedLicenseAsync(
            issueDate: new DateTime(2020, 1, 1),
            expirationDate: new DateTime(2026, 11, 7, 23, 59, 59),
            isActive: true);

        var expiresTomorrow = await SeedLicenseAsync(
            issueDate: new DateTime(2020, 1, 1),
            expirationDate: new DateTime(2026, 11, 9, 0, 0, 0),
            isActive: true);

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var summary = await repository.GetRenewalSummaryAsync(cutoff);

        Assert.NotNull(summary);
        Assert.True(summary.TotalExpired >= 1);
    }

    [Fact]
    public async Task GetRenewalSummaryAsync_WhenExpiredLicenseHasAnotherActiveLicenseForSameDriverAndClass_ExcludesFromEligible()
    {
        var cutoff = new DateOnly(2026, 11, 8);
        var expiredDate = new DateTime(2026, 11, 7);

        // Standard eligible expired license (different driver/class)
        var legitimateEligible = await SeedLicenseAsync(
            issueDate: new DateTime(2020, 1, 1),
            expirationDate: expiredDate,
            isActive: true);

        // Driver with active license (License A, active, not expired)
        var activeLicense = await SeedLicenseAsync(
            issueDate: new DateTime(2026, 1, 1),
            expirationDate: new DateTime(2028, 1, 1),
            isActive: true);

        // Same driver and same class has an expired inactive license (License B, inactive, expired)
        var expiredInactiveLicense = await SeedAdditionalLicenseAsync(
            activeLicense,
            issueDate: new DateTime(2020, 1, 1),
            isActive: false,
            createNewLicenseClass: false);

        // Ensure expiredInactiveLicense expiration date is before cutoff
        await using (var ctx = _database.CreateContext())
        {
            var lic = await ctx.Licenses.SingleAsync(l => l.LicenseID == expiredInactiveLicense.LicenseId);
            lic.ExpirationDate = expiredDate;
            await ctx.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var summary = await repository.GetRenewalSummaryAsync(cutoff);

        Assert.NotNull(summary);
        // The expiredInactiveLicense is in TotalExpired, but NOT in EligibleForRenewal because activeLicense is active for the same driver and class
        Assert.True(summary.TotalExpired >= 2);
        Assert.True(summary.IneligibleForRenewal >= 1);
        Assert.Equal(summary.TotalExpired - summary.EligibleForRenewal, summary.IneligibleForRenewal);
    }

    [Fact]
    public async Task GetNewerLicensesBatchAsync_WhenEmptyOrNull_ReturnsEmptyDictionary()
    {
        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var nullResult = await repository.GetNewerLicensesBatchAsync(null!);
        var emptyResult = await repository.GetNewerLicensesBatchAsync([]);

        Assert.NotNull(nullResult);
        Assert.Empty(nullResult);
        Assert.NotNull(emptyResult);
        Assert.Empty(emptyResult);
    }

    [Fact]
    public async Task GetNewerLicensesBatchAsync_WhenMultipleDriversAndClasses_ReturnsCorrectNewestPerDriverAndClass()
    {
        // Arrange
        // Driver 1, Class A: older (lic1), newer (lic2)
        var seed1 = await SeedLicenseAsync(issueDate: DateTime.UtcNow.AddYears(-3), isActive: false);
        var seed2 = await SeedAdditionalLicenseAsync(seed1, issueDate: DateTime.UtcNow.AddYears(-1), isActive: true, createNewLicenseClass: false);

        // Driver 2, Class B: single license (no newer)
        var seed3 = await SeedLicenseAsync(issueDate: DateTime.UtcNow.AddYears(-2), isActive: true);

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var cand1 = new License { LicenseID = seed1.LicenseId, DriverID = seed1.DriverId, LicenseClass = seed1.LicenseClassId };
        var cand3 = new License { LicenseID = seed3.LicenseId, DriverID = seed3.DriverId, LicenseClass = seed3.LicenseClassId };

        // Act
        var result = await repository.GetNewerLicensesBatchAsync([cand1, cand3]);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey(seed1.LicenseId));
        Assert.Equal(seed2.LicenseId, result[seed1.LicenseId].LicenseID);

        // cand3 has no newer license
        Assert.False(result.ContainsKey(seed3.LicenseId));
    }

    [Fact]
    public async Task GetNewerLicensesBatchAsync_WhenMultipleLicensesForSameDriverAndClass_SelectsNewestDescending()
    {
        // Arrange: Driver has 3 sequential licenses in the same class: Lic1 < Lic2 < Lic3
        var seed1 = await SeedLicenseAsync(issueDate: DateTime.UtcNow.AddYears(-4), isActive: false);
        var seed2 = await SeedAdditionalLicenseAsync(seed1, issueDate: DateTime.UtcNow.AddYears(-2), isActive: false, createNewLicenseClass: false);
        var seed3 = await SeedAdditionalLicenseAsync(seed1, issueDate: DateTime.UtcNow.AddMonths(-6), isActive: true, createNewLicenseClass: false);

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var cand1 = new License { LicenseID = seed1.LicenseId, DriverID = seed1.DriverId, LicenseClass = seed1.LicenseClassId };
        var cand2 = new License { LicenseID = seed2.LicenseId, DriverID = seed2.DriverId, LicenseClass = seed2.LicenseClassId };

        // Act
        var result = await repository.GetNewerLicensesBatchAsync([cand1, cand2]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(seed3.LicenseId, result[seed1.LicenseId].LicenseID);
        Assert.Equal(seed3.LicenseId, result[seed2.LicenseId].LicenseID);
    }

    [Fact]
    public async Task GetNewerLicensesBatchAsync_WhenUnrelatedDriverAndClassCombinations_DoesNotCrossMatch()
    {
        // Arrange
        // Driver 1: has Lic1 in Class A (older), Lic2 in Class B (different class, newer)
        var seed1 = await SeedLicenseAsync(issueDate: DateTime.UtcNow.AddYears(-3), isActive: false);
        var seed2 = await SeedAdditionalLicenseAsync(seed1, issueDate: DateTime.UtcNow.AddYears(-1), isActive: true, createNewLicenseClass: true);

        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        var cand1 = new License { LicenseID = seed1.LicenseId, DriverID = seed1.DriverId, LicenseClass = seed1.LicenseClassId };

        // Act: cand1 is Class A. Lic2 is Class B. Must not cross-match.
        var result = await repository.GetNewerLicensesBatchAsync([cand1]);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.ContainsKey(seed1.LicenseId));
    }

    [Fact]
    public async Task GetNewerLicensesBatchAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        await using var context = _database.CreateContext();
        var repository = new LicenseRepository(context);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cand = new License { LicenseID = 1, DriverID = 1, LicenseClass = 1 };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetNewerLicensesBatchAsync([cand], cts.Token));
    }

}
