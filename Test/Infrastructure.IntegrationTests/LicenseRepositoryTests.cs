using Application.Common.Pagination;
using Application.DTOs.LicenseDTO;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public sealed partial class LicenseRepositoryTests
    : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public LicenseRepositoryTests(
        SqlServerTestDatabase database)
    {
        _database = database;
    }

    private async Task<LicenseSeed> SeedLicenseAsync(
        DateTime? issueDate = null,
        DateTime? expirationDate = null,
        bool isActive = true,
        int? licenseClassIdOverride = null)
    {
        await using var context =
            _database.CreateContext();

        var uniqueId =
            Guid.NewGuid().ToString("N");

        var country =
            new Country
            {
                CountryName =
                    $"Test Country {uniqueId}"
            };

        context.Countries.Add(country);
        await context.SaveChangesAsync();

        var person =
            new Person
            {
                NationalNo =
                    $"IT{uniqueId[..18]}",
                FirstName = "Integration",
                SecondName = "Test",
                ThirdName = "License",
                LastName = uniqueId[..8],
                DateOfBirth =
                    new DateTime(1990, 1, 1),
                Gender = Gender.Male,
                Address =
                    "Integration Test Address",
                Phone =
                    $"07{uniqueId[..8]}",
                Email =
                    $"{uniqueId}@integration.test",
                NationalityCountryID =
                    country.CountryId
            };

        context.People.Add(person);
        await context.SaveChangesAsync();

        var user =
            new User
            {
                PersonId =
                    person.PersonId,
                UserName =
                    $"test_{uniqueId[..20]}",
                Password =
                    "IntegrationTestPassword",
                IsActive = true,
                Role = UserRole.Staff
            };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var applicationType =
            new ApplicationType
            {
                ApplicationTypeTitle =
                    $"Integration Test Type {uniqueId}",
                ApplicationFees = 10
            };

        context.ApplicationTypes.Add(applicationType);
        await context.SaveChangesAsync();

        var application =
            new ApplicationD
            {
                ApplicantPersonID =
                    person.PersonId,
                ApplicationDate =
                    DateTime.UtcNow,
                ApplicationTypeID =
                    applicationType.ApplicationTypeId,
                ApplicationStatus =
                    AppStatus.New,
                LastStatusDate =
                    DateTime.UtcNow,
                PaidFees = 10,
                CreatedByUserID =
                    user.UserId
            };

        context.Applications.Add(application);
        await context.SaveChangesAsync();

        var driver =
            new Driver
            {
                PersonID =
                    person.PersonId,
                CreatedByUserID =
                    user.UserId,
                CreatedDate =
                    DateTime.UtcNow
            };

        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        var licenseClass =
            licenseClassIdOverride.HasValue
                ? await context.LicenseClasses
                    .SingleAsync(
                        x =>
                            x.LicenseClassID ==
                            licenseClassIdOverride.Value)
                : await CreateLicenseClassAsync(
                    context,
                    uniqueId);

        var license =
            new License
            {
                ApplicationID =
                    application.ApplicationID,
                DriverID =
                    driver.DriverID,
                LicenseClass =
                    licenseClass.LicenseClassID,
                IssueDate =
                    issueDate ??
                    DateTime.UtcNow,
                ExpirationDate =
                    expirationDate ??
                    DateTime.UtcNow.AddYears(1),
                Notes =
                    "Integration Test License",
                PaidFees = 100,
                IsActive =
                    isActive,
                IssueReason =
                    IssueReason.FirstTime,
                CreatedByUserID =
                    user.UserId
            };

        context.Licenses.Add(license);
        await context.SaveChangesAsync();

        return new LicenseSeed(
            license.LicenseID,
            application.ApplicationID,
            driver.DriverID,
            licenseClass.LicenseClassID,
            person.PersonId,
            user.UserId);
    }

    private async Task<LicenseSeed> SeedAdditionalLicenseAsync(
        LicenseSeed source,
        DateTime? issueDate = null,
        bool isActive = false,
        bool createNewLicenseClass = false)
    {
        await using var context =
            _database.CreateContext();

        var uniqueId =
            Guid.NewGuid().ToString("N");

        var applicationType =
            new ApplicationType
            {
                ApplicationTypeTitle =
                    $"Additional Test Type {uniqueId}",
                ApplicationFees = 10
            };

        context.ApplicationTypes.Add(applicationType);
        await context.SaveChangesAsync();

        var application =
            new ApplicationD
            {
                ApplicantPersonID =
                    source.PersonId,
                ApplicationDate =
                    DateTime.UtcNow,
                ApplicationTypeID =
                    applicationType.ApplicationTypeId,
                ApplicationStatus =
                    AppStatus.New,
                LastStatusDate =
                    DateTime.UtcNow,
                PaidFees = 10,
                CreatedByUserID =
                    source.UserId
            };

        context.Applications.Add(application);
        await context.SaveChangesAsync();

        var licenseClass =
            createNewLicenseClass
                ? await CreateLicenseClassAsync(
                    context,
                    uniqueId)
                : await context.LicenseClasses
                    .SingleAsync(
                        x =>
                            x.LicenseClassID ==
                            source.LicenseClassId);

        var license =
            new License
            {
                ApplicationID =
                    application.ApplicationID,
                DriverID =
                    source.DriverId,
                LicenseClass =
                    licenseClass.LicenseClassID,
                IssueDate =
                    issueDate ??
                    DateTime.UtcNow,
                ExpirationDate =
                    DateTime.UtcNow.AddYears(1),
                Notes =
                    "Additional Integration Test License",
                PaidFees = 100,
                IsActive =
                    isActive,
                IssueReason =
                    IssueReason.Renew,
                CreatedByUserID =
                    source.UserId
            };

        context.Licenses.Add(license);
        await context.SaveChangesAsync();

        return new LicenseSeed(
            license.LicenseID,
            application.ApplicationID,
            source.DriverId,
            licenseClass.LicenseClassID,
            source.PersonId,
            source.UserId);
    }

    private async Task<ApplicationSeed> SeedAdditionalApplicationAsync(
        int personId,
        int userId)
    {
        await using var context =
            _database.CreateContext();

        var uniqueId =
            Guid.NewGuid().ToString("N");

        var applicationType =
            new ApplicationType
            {
                ApplicationTypeTitle =
                    $"Add License Type {uniqueId}",
                ApplicationFees = 10
            };

        context.ApplicationTypes.Add(applicationType);
        await context.SaveChangesAsync();

        var application =
            new ApplicationD
            {
                ApplicantPersonID =
                    personId,
                ApplicationDate =
                    DateTime.UtcNow,
                ApplicationTypeID =
                    applicationType.ApplicationTypeId,
                ApplicationStatus =
                    AppStatus.New,
                LastStatusDate =
                    DateTime.UtcNow,
                PaidFees = 10,
                CreatedByUserID =
                    userId
            };

        context.Applications.Add(application);
        await context.SaveChangesAsync();

        return new ApplicationSeed(
            application.ApplicationID);
    }

    private static async Task<LicenseClass> CreateLicenseClassAsync(
        DVLDDbContext context,
        string uniqueId)
    {
        var licenseClass =
            new LicenseClass
            {
                ClassName =
                    $"Integration Test Class {uniqueId}",
                ClassDescription =
                    "Integration test license class",
                MinimumAllowedAge = 18,
                DefaultValidityLength = 10,
                ClassFees = 100
            };

        context.LicenseClasses.Add(licenseClass);

        await context.SaveChangesAsync();

        return licenseClass;
    }

    private sealed record LicenseSeed(
        int LicenseId,
        int ApplicationId,
        int DriverId,
        int LicenseClassId,
        int PersonId,
        int UserId);

    private sealed record ApplicationSeed(
        int ApplicationId);
}
