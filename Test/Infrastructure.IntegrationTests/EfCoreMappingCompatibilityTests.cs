using Domain.Entities;
using Domain.Enums;

using Infrastructure.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.IntegrationTests;

public sealed class EfCoreMappingCompatibilityTests(
    UserRepositoryDatabaseFixture fixture)
    : IClassFixture<UserRepositoryDatabaseFixture>
{
    [Fact]
    public void TestAppointment_UsesSqlOutputClauseFalse_AndUserRole_HasSentinel()
    {
        using var context = fixture.Database.CreateContext();

        var appointmentEntity = context.Model.FindEntityType(typeof(TestAppointment))!;
        Assert.False(appointmentEntity.IsSqlOutputClauseUsed());

        var roleProperty = context.Model.FindEntityType(typeof(User))!
            .FindProperty(nameof(User.Role))!;
        Assert.Equal((UserRole)0, roleProperty.Sentinel);
    }

    [Fact]
    public async Task UserRole_ExplicitAdminAndStaff_AndUnsetDefault_PersistExpectedRoles()
    {
        await using var context = fixture.Database.CreateContext();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var country = await context.Countries.AsNoTracking().FirstAsync();

        var personAdmin = new Person
        {
            NationalNo = $"NAT-A-{suffix}",
            FirstName = "Admin",
            SecondName = "Test",
            LastName = "User",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = 0,
            Address = "Test",
            Phone = "0790000001",
            NationalityCountryID = country.CountryId
        };
        var personStaff = new Person
        {
            NationalNo = $"NAT-S-{suffix}",
            FirstName = "Staff",
            SecondName = "Test",
            LastName = "User",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = 0,
            Address = "Test",
            Phone = "0790000002",
            NationalityCountryID = country.CountryId
        };

        context.People.AddRange(personAdmin, personStaff);
        await context.SaveChangesAsync();

        var adminUser = new User
        {
            PersonId = personAdmin.PersonId,
            UserName = $"admin_{suffix}",
            Password = "hashed-password",
            IsActive = true,
            Role = UserRole.Admin
        };
        var staffUser = new User
        {
            PersonId = personStaff.PersonId,
            UserName = $"staff_{suffix}",
            Password = "hashed-password",
            IsActive = true,
            Role = UserRole.Staff
        };

        context.Users.AddRange(adminUser, staffUser);
        await context.SaveChangesAsync();

        var reloadedAdmin = await context.Users.AsNoTracking()
            .SingleAsync(u => u.UserId == adminUser.UserId);
        var reloadedStaff = await context.Users.AsNoTracking()
            .SingleAsync(u => u.UserId == staffUser.UserId);

        Assert.Equal(UserRole.Admin, reloadedAdmin.Role);
        Assert.Equal(UserRole.Staff, reloadedStaff.Role);
    }

    [Fact]
    public async Task TestAppointment_InsertWithEnabledAfterInsertTrigger_SucceedsWithoutOutputClauseError()
    {
        await using var context = fixture.Database.CreateContext();

        const string triggerName = "TR_TestAppointments_NoOp_CompatibilityTest";
        await context.Database.ExecuteSqlRawAsync(
            $"""
            CREATE TRIGGER {triggerName}
            ON TestAppointments
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
            END
            """);

        try
        {
            var country = await context.Countries.AsNoTracking().FirstAsync();
            var appType = await context.ApplicationTypes.AsNoTracking().FirstOrDefaultAsync();
            if (appType is null)
            {
                appType = new ApplicationType
                {
                    ApplicationTypeTitle = "Compatibility App Type",
                    ApplicationFees = 15m
                };
                context.ApplicationTypes.Add(appType);
                await context.SaveChangesAsync();
            }
            var licenseClass = await context.LicenseClasses.AsNoTracking().FirstOrDefaultAsync();
            if (licenseClass is null)
            {
                licenseClass = new LicenseClass
                {
                    ClassName = "Compatibility Class",
                    ClassDescription = "Compatibility license class",
                    MinimumAllowedAge = 18,
                    DefaultValidityLength = 10,
                    ClassFees = 20m
                };
                context.LicenseClasses.Add(licenseClass);
                await context.SaveChangesAsync();
            }
            var testType = await context.TestTypes.AsNoTracking().FirstOrDefaultAsync();
            if (testType is null)
            {
                testType = new TestType
                {
                    TestTypeTitle = "Compatibility Vision Test",
                    TestTypeDescription = "Compatibility test type",
                    TestTypeFees = 10m
                };
                context.TestTypes.Add(testType);
                await context.SaveChangesAsync();
            }

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var person = new Person
            {
                NationalNo = $"NAT-T-{suffix}",
                FirstName = "Trigger",
                SecondName = "Test",
                LastName = "Person",
                DateOfBirth = new DateTime(1992, 1, 1),
                Gender = 0,
                Address = "Test",
                Phone = "0790000003",
                NationalityCountryID = country.CountryId
            };
            context.People.Add(person);
            await context.SaveChangesAsync();

            var user = new User
            {
                PersonId = person.PersonId,
                UserName = $"trig_{suffix}",
                Password = "hashed-password",
                IsActive = true,
                Role = UserRole.Staff
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var app = new ApplicationD
            {
                ApplicantPersonID = person.PersonId,
                ApplicationDate = DateTime.UtcNow,
                ApplicationTypeID = appType.ApplicationTypeId,
                ApplicationStatus = Domain.Enums.AppStatus.New,
                LastStatusDate = DateTime.UtcNow,
                PaidFees = appType.ApplicationFees,
                CreatedByUserID = user.UserId
            };
            context.Applications.Add(app);
            await context.SaveChangesAsync();

            var localApp = new LocalDrivingLicenseApplication
            {
                ApplicationID = app.ApplicationID,
                LicenseClassID = licenseClass.LicenseClassID
            };
            context.LocalDrivingLicenseApplications.Add(localApp);
            await context.SaveChangesAsync();

            var appointment = new TestAppointment
            {
                TestTypeID = testType.TestTypeId,
                LocalDrivingLicenseApplicationID = localApp.LocalDrivingLicenseApplicationID,
                AppointmentDate = DateTime.UtcNow.AddDays(3),
                PaidFees = testType.TestTypeFees,
                CreatedByUserID = user.UserId,
                IsLocked = false,
                Status = AppointmentStatus.Scheduled
            };
            context.TestAppointments.Add(appointment);
            await context.SaveChangesAsync();

            Assert.True(appointment.TestAppointmentID > 0);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(
                $"DROP TRIGGER IF EXISTS {triggerName}");
        }
    }
}
