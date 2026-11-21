using Application.Mappers;
using Domain.Entities;
using Domain.Enums;

namespace Application.UnitTests.Mappings;

public class MapperCleanupTests
{
    [Theory]
    [InlineData("Ahmad", "Sami", "Ali", "Hassan", "Ahmad Sami Ali Hassan")]
    [InlineData("Ahmad", "Sami", null, "Hassan", "Ahmad Sami Hassan")]
    [InlineData("Ahmad", "Sami", "", "Hassan", "Ahmad Sami Hassan")]
    [InlineData("Ahmad", "Sami", "   ", "Hassan", "Ahmad Sami Hassan")]
    [InlineData("Ahmad", "", null, "Hassan", "Ahmad Hassan")]
    public void FullName_UserMapperAndLocalAppMapper_MatchPersonFullNameAcrossAllEdgeCases(
        string first,
        string second,
        string? third,
        string last,
        string expected)
    {
        var person = new Person
        {
            PersonId = 1,
            FirstName = first,
            SecondName = second,
            ThirdName = third,
            LastName = last,
            NationalNo = "N1"
        };

        var user = new User
        {
            UserId = 10,
            PersonId = 1,
            UserName = "tester",
            IsActive = true,
            Role = UserRole.Staff,
            Person = person
        };

        var localApp = new LocalDrivingLicenseApplication
        {
            LocalDrivingLicenseApplicationID = 5,
            ApplicationID = 100,
            LicenseClassID = 3,
            Application = new ApplicationD
            {
                ApplicationID = 100,
                ApplicantPersonID = 1,
                ApplicationDate = new DateTime(2026, 1, 1),
                ApplicationStatus = AppStatus.New,
                PaidFees = 15,
                Person = person
            },
            LicenseClass = new LicenseClass
            {
                LicenseClassID = 3,
                ClassName = "Class 3",
                ClassFees = 20
            }
        };

        Assert.Equal(expected, person.FullName);
        Assert.Equal(person.FullName, UserMapper.ToDto(user).FullName);
        Assert.Equal(person.FullName, UserMapper.ToProfileDto(user).FullName);
        Assert.Equal(person.FullName, LocalDrivingLicenseApplicationMapper.ToDto(localApp, 0, false).FullName);
    }

    [Fact]
    public void FullName_WhenPersonIsNull_ReturnsEmptyStringWithoutSpaces()
    {
        var user = new User
        {
            UserId = 10,
            PersonId = 1,
            UserName = "tester",
            IsActive = true,
            Role = UserRole.Staff,
            Person = null!
        };

        var localApp = new LocalDrivingLicenseApplication
        {
            LocalDrivingLicenseApplicationID = 5,
            ApplicationID = 100,
            LicenseClassID = 3,
            Application = new ApplicationD
            {
                ApplicationID = 100,
                ApplicantPersonID = 1,
                ApplicationDate = new DateTime(2026, 1, 1),
                ApplicationStatus = AppStatus.New,
                PaidFees = 15,
                Person = null!
            }
        };

        Assert.Equal(string.Empty, UserMapper.ToDto(user).FullName);
        Assert.Equal(string.Empty, UserMapper.ToProfileDto(user).FullName);
        Assert.Equal(string.Empty, LocalDrivingLicenseApplicationMapper.ToDto(localApp, 0, false).FullName);
    }

    [Fact]
    public void RowVersion_PersonMapperAndUserMapper_EncodeIdenticallyAcrossNullEmptyAndBytes()
    {
        var sampleBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var expectedBase64 = Convert.ToBase64String(sampleBytes);

        var personNull = new Person { PersonId = 1, RowVersion = null! };
        var personEmpty = new Person { PersonId = 2, RowVersion = [] };
        var personBytes = new Person { PersonId = 3, RowVersion = sampleBytes };

        Assert.Equal(string.Empty, PersonMapper.ToDto(personNull).RowVersion);
        Assert.Equal(string.Empty, PersonMapper.ToDto(personEmpty).RowVersion);
        Assert.Equal(expectedBase64, PersonMapper.ToDto(personBytes).RowVersion);

        Assert.Equal(
            PersonMapper.ToDto(personNull).RowVersion,
            UserMapper.ToProfileDto(new User { UserId = 1, Person = personNull }).RowVersion);
        Assert.Equal(
            PersonMapper.ToDto(personEmpty).RowVersion,
            UserMapper.ToProfileDto(new User { UserId = 2, Person = personEmpty }).RowVersion);
        Assert.Equal(
            PersonMapper.ToDto(personBytes).RowVersion,
            UserMapper.ToProfileDto(new User { UserId = 3, Person = personBytes }).RowVersion);
    }

    [Fact]
    public void CentralizedReferenceMappers_MapAllPropertiesExactly()
    {
        var appType = new ApplicationType
        {
            ApplicationTypeId = 1,
            ApplicationTypeTitle = "New Local License",
            ApplicationFees = 15m
        };
        var appTypeDto = ApplicationTypeMapper.ToDto(appType);
        Assert.Equal(1, appTypeDto.ApplicationTypeId);
        Assert.Equal("New Local License", appTypeDto.ApplicationTypeTitle);
        Assert.Equal(15m, appTypeDto.ApplicationTypeFees);

        var testType = new TestType
        {
            TestTypeId = 2,
            TestTypeTitle = "Written Test",
            TestTypeDescription = "Theory exam",
            TestTypeFees = 20m
        };
        var testTypeDto = TestTypeMapper.ToDto(testType);
        Assert.Equal(2, testTypeDto.TestTypeId);
        Assert.Equal("Written Test", testTypeDto.TestTypeTitle);
        Assert.Equal("Theory exam", testTypeDto.TestTypeDescription);
        Assert.Equal(20m, testTypeDto.TestTypeFees);

        var licenseClass = new LicenseClass
        {
            LicenseClassID = 3,
            ClassName = "Ordinary",
            ClassDescription = "Standard car",
            MinimumAllowedAge = 18,
            DefaultValidityLength = 10,
            ClassFees = 25m
        };
        var licenseClassDto = LicenseClassMapper.ToDto(licenseClass);
        Assert.Equal(3, licenseClassDto.LicenseClassID);
        Assert.Equal("Ordinary", licenseClassDto.LicenseClassName);
        Assert.Equal("Standard car", licenseClassDto.LicenseClassDescription);
        Assert.Equal((byte)18, licenseClassDto.MinAllowedAge);
        Assert.Equal((byte)10, licenseClassDto.DefaultValidityLength);
        Assert.Equal(25m, licenseClassDto.LicenseClassFees);
    }
}
