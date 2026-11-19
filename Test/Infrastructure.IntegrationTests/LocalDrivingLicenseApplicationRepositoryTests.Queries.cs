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
    public async Task GetAllAsync_ShouldReturnAllLocalApplications()
    {
        var initialCount = await GetLocalApplicationCountAsync();

        var first = await SeedLocalApplicationAsync();
        var second = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetAllAsync(new PaginationRequest { PageNumber = 1, PageSize = 100 });

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(initialCount + 2, result.TotalCount);

        Assert.Contains(result.Items, x => x.LocalDrivingLicenseApplicationID == first.LocalApplicationId);

        Assert.Contains(result.Items, x => x.LocalDrivingLicenseApplicationID == second.LocalApplicationId);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnRequestedPage()
    {
        var initialCount = await GetLocalApplicationCountAsync();

        await SeedLocalApplicationAsync();
        await SeedLocalApplicationAsync();
        var third = await SeedLocalApplicationAsync();

        var totalCount = initialCount + 3;
        const int pageSize = 2;
        var lastPage = (int)Math.Ceiling(totalCount / (double)pageSize);

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetAllAsync(new PaginationRequest
        {
            PageNumber = lastPage,
            PageSize = pageSize
        });

        var expectedItemsCount = Math.Min(
            pageSize,
            totalCount - ((lastPage - 1) * pageSize));

        Assert.Equal(lastPage, result.PageNumber);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(totalCount, result.TotalCount);
        Assert.Equal(expectedItemsCount, result.Items.Count);

        Assert.Contains(result.Items,
            x => x.LocalDrivingLicenseApplicationID == third.LocalApplicationId);
    }

    [Fact]
    public async Task GetAllAsync_ShouldLoadApplicationPersonAndLicenseClass()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new PaginationRequest { PageNumber = 1, PageSize = 10 };

        var result = await repository.GetAllAsync(request);

        var entity = Assert.Single(result.Items, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.NotNull(entity.Application);

        Assert.Equal(seed.ApplicationId, entity.Application.ApplicationID);

        Assert.NotNull(entity.Application.Person);

        Assert.Equal(seed.PersonId, entity.Application.Person.PersonId);

        Assert.NotNull(entity.LicenseClass);

        Assert.Equal(seed.LicenseClassId, entity.LicenseClass.LicenseClassID);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnDetachedEntities()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetAllAsync(new PaginationRequest { PageNumber = 1, PageSize = 100 });

        var entity = Assert.Single(result.Items, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.Equal(EntityState.Detached, context.Entry(entity).State);
    }

    [Fact]
    public async Task GetAllAsync_WhenSearchingByNationalNo_ReturnsMatchingApplications()
    {
        var first = await SeedLocalApplicationAsync();
        var second = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var firstPerson = await context.People.AsNoTracking().SingleAsync(p => p.PersonId == first.PersonId);
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = firstPerson.NationalNo,
            SearchBy = "nationalno"
        };

        var result = await repository.GetAllAsync(request);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(first.LocalApplicationId, item.LocalDrivingLicenseApplicationID);
    }

    [Fact]
    public async Task GetAllAsync_WhenRecordIsBeyondFirstPage_FindsRecordOnPageOneWhenFiltered()
    {
        await SeedLocalApplicationAsync();
        await SeedLocalApplicationAsync();
        var target = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var targetPerson = await context.People.AsNoTracking().SingleAsync(p => p.PersonId == target.PersonId);
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 1,
            SearchQuery = targetPerson.NationalNo,
            SearchBy = "nationalno"
        };

        var result = await repository.GetAllAsync(request);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        var item = Assert.Single(result.Items);
        Assert.Equal(target.LocalApplicationId, item.LocalDrivingLicenseApplicationID);
    }

    [Fact]
    public async Task GetAllAsync_WhenSearchingByFullName_MatchesApplicant()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var person = await context.People.SingleAsync(p => p.PersonId == seed.PersonId);
        var uniqueName = $"Tariq_{Guid.NewGuid():N}";
        person.FirstName = uniqueName;
        await context.SaveChangesAsync();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = uniqueName,
            SearchBy = "name"
        };

        var result = await repository.GetAllAsync(request);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(seed.LocalApplicationId, item.LocalDrivingLicenseApplicationID);
    }

    [Fact]
    public async Task GetAllAsync_WhenSearchingByLocalApplicationId_MatchesExactRecord()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = seed.LocalApplicationId.ToString(),
            SearchBy = "appid"
        };

        var result = await repository.GetAllAsync(request);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(seed.LocalApplicationId, item.LocalDrivingLicenseApplicationID);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteringByStatus_ReturnsOnlyMatchingStatus()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var app = await context.Applications.SingleAsync(a => a.ApplicationID == seed.ApplicationId);
        app.ApplicationStatus = AppStatus.Cancelled;
        await context.SaveChangesAsync();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 50,
            StatusFilter = "Cancelled"
        };

        var result = await repository.GetAllAsync(request);

        Assert.True(result.TotalCount >= 1);
        Assert.All(result.Items, item => Assert.Equal(AppStatus.Cancelled, item.Application.ApplicationStatus));
        Assert.Contains(result.Items, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteringByLicenseClassId_ReturnsOnlyMatchingClass()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 50,
            LicenseClassId = seed.LicenseClassId
        };

        var result = await repository.GetAllAsync(request);

        Assert.True(result.TotalCount >= 1);
        Assert.All(result.Items, item => Assert.Equal(seed.LicenseClassId, item.LicenseClassID));
        Assert.Contains(result.Items, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);
    }

    [Fact]
    public async Task GetAllAsync_WhenSearchHasNoMatch_ReturnsEmptyPageWithZeroTotalCount()
    {
        await using var context = _database.CreateContext();
        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var request = new LocalDrivingLicenseApplicationQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = "NON_EXISTENT_QUERY_XYZ_9999",
            SearchBy = "all"
        };

        var result = await repository.GetAllAsync(request);

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnExistingEntity()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByIdAsync(seed.LocalApplicationId);

        Assert.NotNull(result);

        Assert.Equal(seed.LocalApplicationId, result.LocalDrivingLicenseApplicationID);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullForMissingId()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByIdAsync(int.MaxValue);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByIdAsync_ShouldReturnNullForInvalidId(int id)
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByIdAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldLoadExpectedNavigationProperties()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByIdAsync(seed.LocalApplicationId);

        Assert.NotNull(result);
        Assert.NotNull(result.Application);
        Assert.NotNull(result.Application.Person);
        Assert.NotNull(result.LicenseClass);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDetachedEntity()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByIdAsync(seed.LocalApplicationId);

        Assert.NotNull(result);

        Assert.Equal(EntityState.Detached, context.Entry(result).State);
    }

    [Fact]
    public async Task GetForUpdateAsync_ShouldReturnExistingEntity()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetForUpdateAsync(seed.LocalApplicationId);

        Assert.NotNull(result);

        Assert.Equal(seed.LocalApplicationId, result.LocalDrivingLicenseApplicationID);
    }

    [Fact]
    public async Task GetForUpdateAsync_ShouldTrackEntity()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetForUpdateAsync(seed.LocalApplicationId);

        Assert.NotNull(result);

        Assert.Equal(EntityState.Unchanged, context.Entry(result).State);
    }

    [Fact]
    public async Task GetForUpdateAsync_ShouldLoadApplication()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetForUpdateAsync(seed.LocalApplicationId);

        Assert.NotNull(result);
        Assert.NotNull(result.Application);

        Assert.Equal(seed.ApplicationId, result.Application.ApplicationID);
    }

    [Fact]
    public async Task GetForUpdateAsync_ShouldAllowModificationAndSave()
    {
        var seed = await SeedLocalApplicationAsync();

        var newLicenseClass = await CreateLicenseClassAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetForUpdateAsync(seed.LocalApplicationId);

        Assert.NotNull(result);

        result.LicenseClassID = newLicenseClass.LicenseClassId;

        await context.SaveChangesAsync();

        await using var verificationContext = _database.CreateContext();

        var persisted = await verificationContext.LocalDrivingLicenseApplications.AsNoTracking().SingleAsync(x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.Equal(newLicenseClass.LicenseClassId, persisted.LicenseClassID);
    }

    [Fact]
    public async Task GetForUpdateAsync_ShouldReturnNullForMissingId()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetForUpdateAsync(int.MaxValue);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetForUpdateAsync_ShouldReturnNullForInvalidId(int id)
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetForUpdateAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByPersonIdAsync_ShouldReturnOnlyApplicationsForPerson()
    {
        var first = await SeedLocalApplicationAsync();

        var second = await SeedLocalApplicationForExistingPersonAsync(first.PersonId);

        var other = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByPersonIdAsync(first.PersonId);

        Assert.Contains(result, x => x.LocalDrivingLicenseApplicationID == first.LocalApplicationId);

        Assert.Contains(result, x => x.LocalDrivingLicenseApplicationID == second.LocalApplicationId);

        Assert.DoesNotContain(result, x => x.LocalDrivingLicenseApplicationID == other.LocalApplicationId);
    }

    [Fact]
    public async Task GetByPersonIdAsync_ShouldLoadExpectedNavigationProperties()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByPersonIdAsync(seed.PersonId);

        var entity = Assert.Single(result, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.NotNull(entity.Application);
        Assert.NotNull(entity.Application.Person);
        Assert.NotNull(entity.LicenseClass);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByPersonIdAsync_ShouldReturnEmptyForInvalidPersonId(int personId)
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByPersonIdAsync(personId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByPersonIdAsync_ShouldReturnDetachedEntities()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByPersonIdAsync(seed.PersonId);

        Assert.Contains(result, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.All(result, entity => Assert.Equal(EntityState.Detached, context.Entry(entity).State));
    }

    [Fact]
    public async Task GetByApplicationIdAsync_ShouldReturnMatchingApplication()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByApplicationIdAsync(seed.ApplicationId);

        var entity = Assert.Single(result);

        Assert.Equal(seed.LocalApplicationId, entity.LocalDrivingLicenseApplicationID);
    }

    [Fact]
    public async Task GetByApplicationIdAsync_ShouldReturnEmptyForMissingApplication()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByApplicationIdAsync(int.MaxValue);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByApplicationIdAsync_ShouldReturnEmptyForInvalidApplicationId(int applicationId)
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByApplicationIdAsync(applicationId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByApplicationIdAsync_ShouldReturnDetachedEntities()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByApplicationIdAsync(seed.ApplicationId);

        Assert.Single(result);

        Assert.All(result, entity => Assert.Equal(EntityState.Detached, context.Entry(entity).State));
    }

    [Fact]
    public async Task GetByLicenseClassIdAsync_ShouldReturnMatchingApplications()
    {
        var first = await SeedLocalApplicationAsync();

        var second = await SeedLocalApplicationAsync(licenseClassId: first.LicenseClassId);

        var other = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByLicenseClassIdAsync(first.LicenseClassId);

        Assert.Contains(result, x => x.LocalDrivingLicenseApplicationID == first.LocalApplicationId);

        Assert.Contains(result, x => x.LocalDrivingLicenseApplicationID == second.LocalApplicationId);

        Assert.DoesNotContain(result, x => x.LocalDrivingLicenseApplicationID == other.LocalApplicationId);
    }

    [Fact]
    public async Task GetByLicenseClassIdAsync_ShouldLoadLicenseClass()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByLicenseClassIdAsync(seed.LicenseClassId);

        var entity = Assert.Single(result, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.NotNull(entity.LicenseClass);

        Assert.Equal(seed.LicenseClassId, entity.LicenseClass.LicenseClassID);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByLicenseClassIdAsync_ShouldReturnEmptyForInvalidId(int licenseClassId)
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByLicenseClassIdAsync(licenseClassId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByLicenseClassIdAsync_ShouldReturnDetachedEntities()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetByLicenseClassIdAsync(seed.LicenseClassId);

        Assert.Contains(result, x => x.LocalDrivingLicenseApplicationID == seed.LocalApplicationId);

        Assert.All(result, entity => Assert.Equal(EntityState.Detached, context.Entry(entity).State));
    }

    [Fact]
    public async Task GetApplicationIdByLocalIdAsync_ShouldReturnApplicationId()
    {
        var seed = await SeedLocalApplicationAsync();

        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetApplicationIdByLocalIdAsync(seed.LocalApplicationId);

        Assert.Equal(seed.ApplicationId, result);
    }

    [Fact]
    public async Task GetApplicationIdByLocalIdAsync_ShouldReturnNullForMissingId()
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetApplicationIdByLocalIdAsync(int.MaxValue);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetApplicationIdByLocalIdAsync_ShouldReturnNullForInvalidId(int localId)
    {
        await using var context = _database.CreateContext();

        var repository = new LocalDrivingLicenseApplicationRepository(context);

        var result = await repository.GetApplicationIdByLocalIdAsync(localId);

        Assert.Null(result);
    }

}
