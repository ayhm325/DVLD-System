using Application.Common.Pagination;
using Application.DTOs.LocalDrivingLicenseApplicationDTO;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public sealed partial class LocalDrivingLicenseApplicationRepositoryTests
    : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public LocalDrivingLicenseApplicationRepositoryTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void Constructor_ShouldThrowWhenContextIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalDrivingLicenseApplicationRepository(null!));
    }

    private async Task<LocalApplicationSeed>
    SeedLocalApplicationAsync(AppStatus applicationStatus = AppStatus.New, int? licenseClassId = null)
    {
        await using var context = _database.CreateContext();

        var suffix = Guid.NewGuid().ToString("N");

        // COUNTRY

        var country = new Country { CountryName = $"LDA Country {suffix}" };

        context.Countries.Add(country);

        await context.SaveChangesAsync();

        // PERSON

        var person = new Person
        {
            NationalNo = $"LDA{suffix[..17]}",
            FirstName = "Local",
            SecondName = "Driving",
            ThirdName = "Application",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            Address = "Test Address",
            Phone = $"079{Random.Shared.Next(1000000, 9999999)}",
            Email = $"{suffix}@example.com",
            NationalityCountryID = country.CountryId
        };

        context.People.Add(person);

        await context.SaveChangesAsync();

        // USER

        var user = new User { PersonId = person.PersonId, UserName = $"lda_user_{suffix}", Password = "TestPassword", IsActive = true, Role = UserRole.Staff };

        context.Users.Add(user);

        await context.SaveChangesAsync();

        // APPLICATION TYPE

        var applicationType = new ApplicationType { ApplicationTypeTitle = $"LDA Application {suffix}", ApplicationFees = 50m };

        context.ApplicationTypes.Add(applicationType);

        await context.SaveChangesAsync();

        // LICENSE CLASS

        LicenseClass licenseClass;

        if (licenseClassId.HasValue)
        {
            licenseClass = await context.LicenseClasses.FirstAsync(x => x.LicenseClassID == licenseClassId.Value);
        }
        else
        {
            licenseClass = new LicenseClass { ClassName = $"LDA Class {suffix}", ClassDescription = "Integration test license class", MinimumAllowedAge = 18, DefaultValidityLength = 5, ClassFees = 100m };

            context.LicenseClasses.Add(licenseClass);

            await context.SaveChangesAsync();
        }
        // APPLICATION

        var application = new ApplicationD { ApplicantPersonID = person.PersonId, ApplicationDate = DateTime.UtcNow, ApplicationTypeID = applicationType.ApplicationTypeId, ApplicationStatus = applicationStatus, LastStatusDate = DateTime.UtcNow, PaidFees = applicationType.ApplicationFees, CreatedByUserID = user.UserId };

        context.Applications.Add(application);

        await context.SaveChangesAsync();

        // LOCAL APPLICATION

        var localApplication = new LocalDrivingLicenseApplication { ApplicationID = application.ApplicationID, LicenseClassID = licenseClass.LicenseClassID };

        context.LocalDrivingLicenseApplications.Add(localApplication);

        await context.SaveChangesAsync();

        return new LocalApplicationSeed { LocalApplicationId = localApplication.LocalDrivingLicenseApplicationID, ApplicationId = application.ApplicationID, PersonId = person.PersonId, LicenseClassId = licenseClass.LicenseClassID, CreatedByUserId = user.UserId };
    }
    private async Task<LocalApplicationSeed>
    SeedLocalApplicationForExistingPersonAsync(int personId)
    {
        await using var context = _database.CreateContext();

        var person = await context.People.AsNoTracking().FirstAsync(x => x.PersonId == personId);

        var user = await context.Users.AsNoTracking().FirstAsync(x => x.PersonId == personId);

        var applicationType = await context.ApplicationTypes.AsNoTracking().FirstAsync();
        var licenseClass = await CreateLicenseClassUsingContextAsync(context);

        var application = new ApplicationD { ApplicantPersonID = person.PersonId, ApplicationDate = DateTime.UtcNow, ApplicationTypeID = applicationType.ApplicationTypeId, ApplicationStatus = AppStatus.New, LastStatusDate = DateTime.UtcNow, PaidFees = applicationType.ApplicationFees, CreatedByUserID = user.UserId };

        context.Applications.Add(application);

        await context.SaveChangesAsync();

        var localApplication = new LocalDrivingLicenseApplication { ApplicationID = application.ApplicationID, LicenseClassID = licenseClass.LicenseClassId };

        context.LocalDrivingLicenseApplications.Add(localApplication);

        await context.SaveChangesAsync();

        return new LocalApplicationSeed { LocalApplicationId = localApplication.LocalDrivingLicenseApplicationID, ApplicationId = application.ApplicationID, PersonId = person.PersonId, LicenseClassId = licenseClass.LicenseClassId, CreatedByUserId = user.UserId };
    }
    private async Task<PersonSeed>
    CreatePersonAsync()
    {
        await using var context = _database.CreateContext();

        var suffix = Guid.NewGuid().ToString("N");

        var country = new Country { CountryName = $"Person Country {suffix}" };

        context.Countries.Add(country);

        await context.SaveChangesAsync();

        var person = new Person
        {
            NationalNo = $"PERSON-{suffix[..13]}",
            FirstName = "Test",
            SecondName = "Person",
            LastName = "ForLDA",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            Address = "Test Address",
            Phone = $"078{Random.Shared.Next(1000000, 9999999)}",
            Email = $"{suffix}@example.com",
            NationalityCountryID = country.CountryId
        };

        context.People.Add(person);

        await context.SaveChangesAsync();

        return new PersonSeed { PersonId = person.PersonId };
    }
    private async Task<LicenseClassSeed>
    CreateLicenseClassAsync()
    {
        await using var context = _database.CreateContext();

        return await CreateLicenseClassUsingContextAsync(context);
    }
    private static async Task<LicenseClassSeed>
    CreateLicenseClassUsingContextAsync(DVLDDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N");

        var licenseClass = new LicenseClass { ClassName = $"Extra Class {suffix}", ClassDescription = "Additional integration test class", MinimumAllowedAge = 18, DefaultValidityLength = 5, ClassFees = 100m };

        context.LicenseClasses.Add(licenseClass);

        await context.SaveChangesAsync();

        return new LicenseClassSeed { LicenseClassId = licenseClass.LicenseClassID };
    }
    private async Task<ApplicationSeed>
    CreateApplicationAsync(int personId, int createdByUserId)
    {
        await using var context = _database.CreateContext();

        var applicationType = await context.ApplicationTypes.AsNoTracking().FirstAsync();
        var application = new ApplicationD { ApplicantPersonID = personId, ApplicationDate = DateTime.UtcNow, ApplicationTypeID = applicationType.ApplicationTypeId, ApplicationStatus = AppStatus.New, LastStatusDate = DateTime.UtcNow, PaidFees = applicationType.ApplicationFees, CreatedByUserID = createdByUserId };

        context.Applications.Add(application);

        await context.SaveChangesAsync();

        return new ApplicationSeed { ApplicationId = application.ApplicationID };
    }
    private async Task SeedTestAsync(int localApplicationId, bool testResult, TestTypeEnum? requestedTestType = null)
    {
        await using var context = _database.CreateContext();

        // USER

        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync();
        if (user is null)
        {
            throw new InvalidOperationException("The test database does not contain a User. " + "SeedLocalApplicationAsync should create one.");
        }
        // TEST TYPE

        var testType = requestedTestType.HasValue
            ? await EnsureRequiredTestTypeAsync(context, requestedTestType.Value)
            : await context.TestTypes.FirstOrDefaultAsync();
        if (testType is null)
        {
            testType = new TestType { TestTypeTitle = $"Integration Test {Guid.NewGuid():N}", TestTypeDescription = "Local driving license repository integration test", TestTypeFees = 10m };

            context.TestTypes.Add(testType);

            await context.SaveChangesAsync();
        }
        // APPOINTMENT

        var appointment = new TestAppointment { TestTypeID = testType.TestTypeId, LocalDrivingLicenseApplicationID = localApplicationId, AppointmentDate = DateTime.UtcNow, PaidFees = 10m, CreatedByUserID = user.UserId, IsLocked = true };

        context.TestAppointments.Add(appointment);

        await context.SaveChangesAsync();

        // TEST

        var test = new Test { TestAppointmentID = appointment.TestAppointmentID, TestResult = testResult, Notes = "Repository integration test", CreatedByUserID = user.UserId };

        context.Tests.Add(test);

        await context.SaveChangesAsync();
    }
    private sealed class LocalApplicationSeed
    {
        public int LocalApplicationId { get; init; }

        public int ApplicationId { get; init; }

        public int PersonId { get; init; }

        public int LicenseClassId { get; init; }

        public int CreatedByUserId { get; init; }
    }
    private sealed class PersonSeed
    {
        public int PersonId { get; init; }
    }
    private sealed class LicenseClassSeed
    {
        public int LicenseClassId { get; init; }
    }
    private sealed class ApplicationSeed
    {
        public int ApplicationId { get; init; }
    }
    private async Task SetApplicationStatusAsync(int applicationId, AppStatus status)
    {
        await using var context = _database.CreateContext();
        var app = await context.Applications.FirstAsync(x => x.ApplicationID == applicationId);
        app.ApplicationStatus = status;
        await context.SaveChangesAsync();
    }

    private async Task SeedLicenseForApplicationAsync(
        int applicationId,
        int personId,
        int licenseClassId,
        int userId)
    {
        await using var context = _database.CreateContext();
        var driver = new Driver
        {
            PersonID = personId,
            CreatedByUserID = userId,
            CreatedDate = DateTime.UtcNow
        };
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        context.Licenses.Add(new License
        {
            ApplicationID = applicationId,
            DriverID = driver.DriverID,
            LicenseClass = licenseClassId,
            IssueDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddYears(10),
            PaidFees = 50m,
            IsActive = true,
            IssueReason = IssueReason.FirstTime,
            CreatedByUserID = userId
        });
        await context.SaveChangesAsync();
    }

    private static async Task<TestType> EnsureRequiredTestTypeAsync(DVLDDbContext context, TestTypeEnum targetType)
    {
        var existing = await context.TestTypes.FirstOrDefaultAsync(t => t.TestTypeId == (int)targetType);
        if (existing is not null)
            return existing;

        foreach (var required in new[] { TestTypeEnum.Theory, TestTypeEnum.Written, TestTypeEnum.Practical })
        {
            if (!await context.TestTypes.AnyAsync(t => t.TestTypeId == (int)required))
            {
                context.TestTypes.Add(new TestType
                {
                    TestTypeTitle = required.ToString(),
                    TestTypeDescription = $"{required} required test",
                    TestTypeFees = 10m
                });
                await context.SaveChangesAsync();
            }
        }

        return await context.TestTypes.FirstAsync(t => t.TestTypeId == (int)targetType);
    }

    private async Task SeedNonRequiredTestAsync(int localApplicationId, bool testResult)
    {
        await using var context = _database.CreateContext();
        var user = await context.Users.AsNoTracking().FirstAsync();

        await EnsureRequiredTestTypeAsync(context, TestTypeEnum.Practical);

        var extraType = new TestType
        {
            TestTypeTitle = $"Legacy Extra {Guid.NewGuid():N}",
            TestTypeDescription = "Non-required test type",
            TestTypeFees = 10m
        };
        context.TestTypes.Add(extraType);
        await context.SaveChangesAsync();

        var appointment = new TestAppointment
        {
            TestTypeID = extraType.TestTypeId,
            LocalDrivingLicenseApplicationID = localApplicationId,
            AppointmentDate = DateTime.UtcNow,
            PaidFees = 10m,
            CreatedByUserID = user.UserId,
            IsLocked = true
        };
        context.TestAppointments.Add(appointment);
        await context.SaveChangesAsync();

        context.Tests.Add(new Test
        {
            TestAppointmentID = appointment.TestAppointmentID,
            TestResult = testResult,
            Notes = "Non-required type test",
            CreatedByUserID = user.UserId
        });
        await context.SaveChangesAsync();
    }

    private async Task<int> GetLocalApplicationCountAsync()
    {
        await using var context = _database.CreateContext();

        return await context.LocalDrivingLicenseApplications
        .CountAsync();
    }
}
