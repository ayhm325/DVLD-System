using Application.Common.Pagination;
using Application.DTOs.LocalDrivingLicenseApplicationDTO;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public sealed partial class LocalDrivingLicenseApplicationRepositoryTests
{
    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldCountOnlyPassedTests()
    {
        var seed = await SeedLocalApplicationAsync();

        await SeedTestAsync(seed.LocalApplicationId, true, TestTypeEnum.Theory);

        await SeedTestAsync(seed.LocalApplicationId, true, TestTypeEnum.Written);

        await SeedTestAsync(seed.LocalApplicationId, false, TestTypeEnum.Practical);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([seed.LocalApplicationId]);

        Assert.True(result.ContainsKey(seed.LocalApplicationId));

        Assert.Equal(2, result[seed.LocalApplicationId]);
    }

    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldReturnCountsForMultipleApplications()
    {
        var first = await SeedLocalApplicationAsync();

        var second = await SeedLocalApplicationAsync();

        await SeedTestAsync(first.LocalApplicationId, true, TestTypeEnum.Theory);

        await SeedTestAsync(first.LocalApplicationId, true, TestTypeEnum.Written);

        await SeedTestAsync(second.LocalApplicationId, true, TestTypeEnum.Theory);

        await SeedTestAsync(second.LocalApplicationId, false, TestTypeEnum.Written);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([first.LocalApplicationId, second.LocalApplicationId]);

        Assert.Equal(2, result[first.LocalApplicationId]);

        Assert.Equal(1, result[second.LocalApplicationId]);
    }

    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldIgnoreDuplicateIds()
    {
        var seed = await SeedLocalApplicationAsync();

        await SeedTestAsync(seed.LocalApplicationId, true);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([seed.LocalApplicationId, seed.LocalApplicationId, seed.LocalApplicationId]);

        Assert.Single(result);

        Assert.Equal(1, result[seed.LocalApplicationId]);
    }

    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldIgnoreInvalidIds()
    {
        var seed = await SeedLocalApplicationAsync();

        await SeedTestAsync(seed.LocalApplicationId, true);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([seed.LocalApplicationId, 0, -1]);

        Assert.Single(result);

        Assert.Equal(1, result[seed.LocalApplicationId]);
    }

    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldReturnEmptyForEmptyInput()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldReturnEmptyForOnlyInvalidIds()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([0, -1, -100]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldThrowWhenIdsAreNull()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.GetPassedTestCountsAsync(null!));
    }

    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldNotReturnApplicationsWithoutPassedTests()
    {
        var seed = await SeedLocalApplicationAsync();

        await SeedTestAsync(seed.LocalApplicationId, false);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([seed.LocalApplicationId]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task HasDuplicateApplicationAsync_ShouldReturnApplicationIdForNewApplication()
    {
        var seed = await SeedLocalApplicationAsync(AppStatus.New);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.HasDuplicateApplicationAsync(seed.PersonId, seed.LicenseClassId);

        Assert.Equal(seed.ApplicationId, result);
    }

    [Fact]
    public async Task HasDuplicateApplicationAsync_ShouldReturnApplicationIdForCompletedApplication()
    {
        var seed = await SeedLocalApplicationAsync(AppStatus.Completed);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.HasDuplicateApplicationAsync(seed.PersonId, seed.LicenseClassId);

        Assert.Equal(seed.ApplicationId, result);
    }

    [Fact]
    public async Task HasDuplicateApplicationAsync_ShouldIgnoreCancelledApplication()
    {
        var seed = await SeedLocalApplicationAsync(AppStatus.Cancelled);

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.HasDuplicateApplicationAsync(seed.PersonId, seed.LicenseClassId);

        Assert.Null(result);
    }

    [Fact]
    public async Task HasDuplicateApplicationAsync_ShouldReturnNullForDifferentPerson()
    {
        var seed = await SeedLocalApplicationAsync();

        var otherPerson = await CreatePersonAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.HasDuplicateApplicationAsync(otherPerson.PersonId, seed.LicenseClassId);

        Assert.Null(result);
    }

    [Fact]
    public async Task HasDuplicateApplicationAsync_ShouldReturnNullForDifferentLicenseClass()
    {
        var seed = await SeedLocalApplicationAsync();

        var otherLicenseClass = await CreateLicenseClassAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.HasDuplicateApplicationAsync(seed.PersonId, otherLicenseClass.LicenseClassId);

        Assert.Null(result);
    }

    [Fact]
    public async Task HasDuplicateApplicationAsync_ShouldReturnNullWhenNoApplicationExists()
    {
        var person = await CreatePersonAsync();

        var licenseClass = await CreateLicenseClassAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.HasDuplicateApplicationAsync(person.PersonId, licenseClass.LicenseClassId);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task HasDuplicateApplicationAsync_ShouldReturnNullForInvalidIds(int personId, int licenseClassId)
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.HasDuplicateApplicationAsync(personId, licenseClassId);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_ShouldThrowWhenEntityIsNull()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!));
    }

    [Fact]
    public async Task AddAsync_ShouldMarkEntityAsAdded()
    {
        var seed = await SeedLocalApplicationAsync();

        var application = await CreateApplicationAsync(seed.PersonId, seed.CreatedByUserId);

        var entity = new LocalDrivingLicenseApplication { ApplicationID = application.ApplicationId, LicenseClassID = seed.LicenseClassId };

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        await repository.AddAsync(entity);

        Assert.Equal(EntityState.Added, context.Entry(entity).State);
    }

    [Fact]
    public async Task AddAsync_ShouldNotPersistUntilSaveChanges()
    {
        var seed = await SeedLocalApplicationAsync();

        var application = await CreateApplicationAsync(seed.PersonId, seed.CreatedByUserId);

        var entity = new LocalDrivingLicenseApplication { ApplicationID = application.ApplicationId, LicenseClassID = seed.LicenseClassId };

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        await repository.AddAsync(entity);

        await using var verificationContext = _database.CreateContext();

        var persistedBeforeSave = await verificationContext.LocalDrivingLicenseApplications.AsNoTracking().AnyAsync(x => x.ApplicationID == application.ApplicationId);

        Assert.False(persistedBeforeSave);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistAfterSaveChanges()
    {
        var seed = await SeedLocalApplicationAsync();

        var application = await CreateApplicationAsync(seed.PersonId, seed.CreatedByUserId);

        var entity = new LocalDrivingLicenseApplication { ApplicationID = application.ApplicationId, LicenseClassID = seed.LicenseClassId };

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        await repository.AddAsync(entity);

        await context.SaveChangesAsync();

        Assert.True(entity.LocalDrivingLicenseApplicationID > 0);

        await using var verificationContext = _database.CreateContext();

        var persisted = await verificationContext.LocalDrivingLicenseApplications.AsNoTracking().SingleOrDefaultAsync(x => x.LocalDrivingLicenseApplicationID == entity.LocalDrivingLicenseApplicationID);

        Assert.NotNull(persisted);

        Assert.Equal(application.ApplicationId, persisted.ApplicationID);

        Assert.Equal(seed.LicenseClassId, persisted.LicenseClassID);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalseForInvalidId()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        Assert.False(await repository.DeleteAsync(0));

        Assert.False(await repository.DeleteAsync(-1));
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalseWhenEntityDoesNotExist()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.DeleteAsync(int.MaxValue);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldMarkExistingEntityAsDeleted()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.DeleteAsync(seed.LocalApplicationId);

        Assert.True(result);

        var entity = await context.LocalDrivingLicenseApplications.FindAsync(seed.LocalApplicationId);

        Assert.NotNull(entity);

        Assert.Equal(EntityState.Deleted, context.Entry(entity).State);
    }

    [Fact]
    public async Task DeleteAsync_ShouldPersistDeletionAfterSaveChanges()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.DeleteAsync(seed.LocalApplicationId);

        Assert.True(result);

        await context.SaveChangesAsync();

        await using var verificationContext = _database.CreateContext();

        var persisted = await verificationContext.LocalDrivingLicenseApplications.AsNoTracking().SingleOrDefaultAsync(x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.Null(persisted);
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotPersistBeforeSaveChanges()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.DeleteAsync(seed.LocalApplicationId);

        Assert.True(result);

        await using var verificationContext = _database.CreateContext();

        var persisted = await verificationContext.LocalDrivingLicenseApplications.AsNoTracking().SingleOrDefaultAsync(x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.NotNull(persisted);
    }
    [Fact]
    public async Task GetPassedTestCountsAsync_ShouldCountDistinctRequiredTestTypesOnly()
    {
        var duplicateTheory = await SeedLocalApplicationAsync();
        await SeedTestAsync(duplicateTheory.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(duplicateTheory.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(duplicateTheory.LocalApplicationId, true, TestTypeEnum.Theory);

        var twoTheoryOneWritten = await SeedLocalApplicationAsync();
        await SeedTestAsync(twoTheoryOneWritten.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(twoTheoryOneWritten.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(twoTheoryOneWritten.LocalApplicationId, true, TestTypeEnum.Written);
        await SeedTestAsync(twoTheoryOneWritten.LocalApplicationId, false, TestTypeEnum.Practical);

        var allThreePlusDuplicate = await SeedLocalApplicationAsync();
        await SeedTestAsync(allThreePlusDuplicate.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(allThreePlusDuplicate.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(allThreePlusDuplicate.LocalApplicationId, true, TestTypeEnum.Written);
        await SeedTestAsync(allThreePlusDuplicate.LocalApplicationId, true, TestTypeEnum.Practical);

        var nonRequiredTypeApp = await SeedLocalApplicationAsync();
        await SeedNonRequiredTestAsync(nonRequiredTypeApp.LocalApplicationId, true);

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetPassedTestCountsAsync([
            duplicateTheory.LocalApplicationId,
            twoTheoryOneWritten.LocalApplicationId,
            allThreePlusDuplicate.LocalApplicationId,
            nonRequiredTypeApp.LocalApplicationId
        ]);

        Assert.Equal(1, result[duplicateTheory.LocalApplicationId]);
        Assert.Equal(2, result[twoTheoryOneWritten.LocalApplicationId]);
        Assert.Equal(3, result[allThreePlusDuplicate.LocalApplicationId]);
        Assert.False(result.ContainsKey(nonRequiredTypeApp.LocalApplicationId));
    }

    [Fact]
    public async Task GetAllAsync_WithWorkflowStage_ClassifiesAndFiltersCorrectly()
    {
        var app0 = await SeedLocalApplicationAsync();

        var app1 = await SeedLocalApplicationAsync();
        await SeedTestAsync(app1.LocalApplicationId, true, TestTypeEnum.Theory);

        var app2 = await SeedLocalApplicationAsync();
        await SeedTestAsync(app2.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(app2.LocalApplicationId, true, TestTypeEnum.Written);

        var appDuplicate3 = await SeedLocalApplicationAsync();
        await SeedTestAsync(appDuplicate3.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(appDuplicate3.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(appDuplicate3.LocalApplicationId, true, TestTypeEnum.Theory);

        var appReady = await SeedLocalApplicationAsync();
        await SeedTestAsync(appReady.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(appReady.LocalApplicationId, true, TestTypeEnum.Written);
        await SeedTestAsync(appReady.LocalApplicationId, true, TestTypeEnum.Practical);

        var appCancelled = await SeedLocalApplicationAsync();
        await SetApplicationStatusAsync(appCancelled.ApplicationId, AppStatus.Cancelled);

        var appCompleted = await SeedLocalApplicationAsync();
        await SeedTestAsync(appCompleted.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(appCompleted.LocalApplicationId, true, TestTypeEnum.Written);
        await SeedTestAsync(appCompleted.LocalApplicationId, true, TestTypeEnum.Practical);
        await SetApplicationStatusAsync(appCompleted.ApplicationId, AppStatus.Completed);

        var appIssued = await SeedLocalApplicationAsync();
        await SeedTestAsync(appIssued.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(appIssued.LocalApplicationId, true, TestTypeEnum.Written);
        await SeedTestAsync(appIssued.LocalApplicationId, true, TestTypeEnum.Practical);
        await SeedLicenseForApplicationAsync(appIssued.ApplicationId, appIssued.PersonId, appIssued.LicenseClassId, appIssued.CreatedByUserId);

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var inProgress = await repository.GetAllAsync(new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 100,
            WorkflowStage = "InProgress"
        });

        Assert.Contains(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == app0.LocalApplicationId);
        Assert.Contains(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == app1.LocalApplicationId);
        Assert.Contains(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == app2.LocalApplicationId);
        Assert.Contains(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == appDuplicate3.LocalApplicationId);
        Assert.DoesNotContain(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == appReady.LocalApplicationId);
        Assert.DoesNotContain(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == appCancelled.LocalApplicationId);
        Assert.DoesNotContain(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == appCompleted.LocalApplicationId);
        Assert.DoesNotContain(inProgress.Items, x => x.LocalDrivingLicenseApplicationID == appIssued.LocalApplicationId);

        var ready = await repository.GetAllAsync(new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 100,
            WorkflowStage = "ReadyForIssuance"
        });

        Assert.Contains(ready.Items, x => x.LocalDrivingLicenseApplicationID == appReady.LocalApplicationId);
        Assert.DoesNotContain(ready.Items, x => x.LocalDrivingLicenseApplicationID == app0.LocalApplicationId);
        Assert.DoesNotContain(ready.Items, x => x.LocalDrivingLicenseApplicationID == appDuplicate3.LocalApplicationId);
        Assert.DoesNotContain(ready.Items, x => x.LocalDrivingLicenseApplicationID == appCancelled.LocalApplicationId);
        Assert.DoesNotContain(ready.Items, x => x.LocalDrivingLicenseApplicationID == appCompleted.LocalApplicationId);
        Assert.DoesNotContain(ready.Items, x => x.LocalDrivingLicenseApplicationID == appIssued.LocalApplicationId);

        var summary = await repository.GetSummaryAsync();
        Assert.True(summary.TotalApplications >= 8);
        Assert.True(summary.InProgressCount >= 4);
        Assert.True(summary.ReadyForIssuanceCount >= 1);
    }

    [Fact]
    public async Task GetAllAsync_WithWorkflowStageAndPaginationSearchLicenseClass_FiltersAccurately()
    {
        var readyA = await SeedLocalApplicationAsync();
        await SeedTestAsync(readyA.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(readyA.LocalApplicationId, true, TestTypeEnum.Written);
        await SeedTestAsync(readyA.LocalApplicationId, true, TestTypeEnum.Practical);

        var readyB = await SeedLocalApplicationAsync();
        await SeedTestAsync(readyB.LocalApplicationId, true, TestTypeEnum.Theory);
        await SeedTestAsync(readyB.LocalApplicationId, true, TestTypeEnum.Written);
        await SeedTestAsync(readyB.LocalApplicationId, true, TestTypeEnum.Practical);

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var byClassAndSearch = await repository.GetAllAsync(new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 1,
            WorkflowStage = "ReadyForIssuance",
            LicenseClassId = readyA.LicenseClassId,
            SearchQuery = readyA.LocalApplicationId.ToString(),
            SearchBy = "appid"
        });

        Assert.Equal(1, byClassAndSearch.TotalCount);
        var item = Assert.Single(byClassAndSearch.Items);
        Assert.Equal(readyA.LocalApplicationId, item.LocalDrivingLicenseApplicationID);
    }

}
