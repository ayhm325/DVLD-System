using Application.Common.Results;
using Application.Common.Time;
using Application.DTOs.LicenseDTO;
using Application.DTOs.TestAppointmentDTO;
using Application.Interfaces;
using Application.Mappers;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Moq;
using Xunit;

namespace Application.UnitTests.Services;

public sealed class ApiDataEfficiencyOptimizationTests
{
    // =========================================================================
    // Target A: Test Appointment DTO Enrichment & Mapping
    // =========================================================================

    [Fact]
    public void TestAppointmentMapper_WhenNavigationsPresent_MapsApplicantFullName_NationalNo_LicenseClassName()
    {
        // Arrange
        var person = new Person
        {
            PersonId = 1,
            NationalNo = "N123456",
            FirstName = "Ahmad",
            SecondName = "Mohammad",
            ThirdName = "Ali",
            LastName = "Al-Jordan"
        };

        var app = new ApplicationD
        {
            ApplicationID = 10,
            ApplicantPersonID = 1,
            Person = person
        };

        var licenseClass = new LicenseClass
        {
            LicenseClassID = 3,
            ClassName = "Class 3 - Ordinary Driving License"
        };

        var localApp = new LocalDrivingLicenseApplication
        {
            LocalDrivingLicenseApplicationID = 42,
            ApplicationID = 10,
            LicenseClassID = 3,
            Application = app,
            LicenseClass = licenseClass
        };

        var appointment = new TestAppointment
        {
            TestAppointmentID = 101,
            TestTypeID = 1,
            LocalDrivingLicenseApplicationID = 42,
            AppointmentDate = new DateTime(2026, 11, 15, 10, 30, 0),
            PaidFees = 20.00m,
            CreatedByUserID = 1,
            IsLocked = false,
            LocalDrivingLicenseApplication = localApp
        };

        // Act
        var dto = TestAppointmentMapper.ToDto(appointment);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(101, dto.TestAppointmentID);
        Assert.Equal(42, dto.LocalDrivingLicenseApplicationID);
        Assert.Equal("Ahmad Mohammad Ali Al-Jordan", dto.ApplicantFullName);
        Assert.Equal("N123456", dto.NationalNo);
        Assert.Equal("Class 3 - Ordinary Driving License", dto.LicenseClassName);
    }

    [Fact]
    public void TestAppointmentMapper_WhenLocalAppOrNavigationsNull_SafelyDefaultsToEmptyStrings()
    {
        // Arrange: Test appointment with no navigation properties loaded
        var appointment = new TestAppointment
        {
            TestAppointmentID = 102,
            TestTypeID = 2,
            LocalDrivingLicenseApplicationID = 55,
            AppointmentDate = new DateTime(2026, 11, 16, 9, 0, 0),
            PaidFees = 20.00m,
            CreatedByUserID = 1,
            IsLocked = false,
            LocalDrivingLicenseApplication = null!
        };

        // Act
        var dto = TestAppointmentMapper.ToDto(appointment);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(102, dto.TestAppointmentID);
        Assert.Equal(string.Empty, dto.ApplicantFullName);
        Assert.Equal(string.Empty, dto.NationalNo);
        Assert.Equal(string.Empty, dto.LicenseClassName);
    }

    [Fact]
    public void TestAppointmentMapper_WhenPersonOrLicenseClassNull_HandlesPartialNavigationsGracefully()
    {
        // Arrange: Local app loaded, but Person and LicenseClass navigations are null
        var localApp = new LocalDrivingLicenseApplication
        {
            LocalDrivingLicenseApplicationID = 60,
            ApplicationID = 12,
            LicenseClassID = 2,
            Application = null!,
            LicenseClass = null!
        };

        var appointment = new TestAppointment
        {
            TestAppointmentID = 103,
            TestTypeID = 3,
            LocalDrivingLicenseApplicationID = 60,
            AppointmentDate = new DateTime(2026, 11, 17, 11, 0, 0),
            PaidFees = 20.00m,
            CreatedByUserID = 1,
            IsLocked = true,
            LocalDrivingLicenseApplication = localApp
        };

        // Act
        var dto = TestAppointmentMapper.ToDto(appointment);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(string.Empty, dto.ApplicantFullName);
        Assert.Equal(string.Empty, dto.NationalNo);
        Assert.Equal(string.Empty, dto.LicenseClassName);
    }

    // =========================================================================
    // Target B: Backend-Filtered License Retrieval (isExpired Filter)
    // =========================================================================

    [Fact]
    public async Task LicenseQueryService_GetAllAsync_WhenIsExpiredTrue_PassesFlagAndJordanTodayToRepository()
    {
        // Arrange
        var mockRepo = new Mock<ILicenseRepository>();
        var mockLocalApp = new Mock<ILocalDrivingLicenseApplicationService>();
        var mockDetained = new Mock<IDetainedLicenseRepository>();
        var mockClock = new Mock<IJordanClock>();

        var testToday = new DateOnly(2026, 11, 7);
        mockClock.SetupGet(c => c.Today).Returns(testToday);

        mockRepo.Setup(r => r.GetAllLicensesAsync(true, testToday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<License>());

        var service = new LicenseQueryService(
            mockRepo.Object,
            mockLocalApp.Object,
            mockDetained.Object,
            mockClock.Object);

        // Act
        var result = await service.GetAllAsync(isExpired: true);

        // Assert
        Assert.True(result.IsSuccess);
        mockRepo.Verify(r => r.GetAllLicensesAsync(true, testToday, It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.GetAllLicensesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LicenseQueryService_GetAllAsync_WhenIsExpiredFalse_PassesFlagAndJordanTodayToRepository()
    {
        // Arrange
        var mockRepo = new Mock<ILicenseRepository>();
        var mockLocalApp = new Mock<ILocalDrivingLicenseApplicationService>();
        var mockDetained = new Mock<IDetainedLicenseRepository>();
        var mockClock = new Mock<IJordanClock>();

        var testToday = new DateOnly(2026, 11, 7);
        mockClock.SetupGet(c => c.Today).Returns(testToday);

        mockRepo.Setup(r => r.GetAllLicensesAsync(false, testToday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<License>());

        var service = new LicenseQueryService(
            mockRepo.Object,
            mockLocalApp.Object,
            mockDetained.Object,
            mockClock.Object);

        // Act
        var result = await service.GetAllAsync(isExpired: false);

        // Assert
        Assert.True(result.IsSuccess);
        mockRepo.Verify(r => r.GetAllLicensesAsync(false, testToday, It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.GetAllLicensesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LicenseQueryService_GetAllAsync_WhenIsExpiredNull_CallsParameterlessRepositoryGetAll()
    {
        // Arrange
        var mockRepo = new Mock<ILicenseRepository>();
        var mockLocalApp = new Mock<ILocalDrivingLicenseApplicationService>();
        var mockDetained = new Mock<IDetainedLicenseRepository>();
        var mockClock = new Mock<IJordanClock>();

        var testToday = new DateOnly(2026, 11, 7);
        mockClock.SetupGet(c => c.Today).Returns(testToday);

        mockRepo.Setup(r => r.GetAllLicensesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<License>());

        var service = new LicenseQueryService(
            mockRepo.Object,
            mockLocalApp.Object,
            mockDetained.Object,
            mockClock.Object);

        // Act
        var result = await service.GetAllAsync(isExpired: null);

        // Assert
        Assert.True(result.IsSuccess);
        mockRepo.Verify(r => r.GetAllLicensesAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.GetAllLicensesAsync(It.IsAny<bool?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LicenseQueryService_GetAllAsync_WhenIsExpiredTrue_ChecksExternalNewerLicenses()
    {
        // Arrange
        var mockRepo = new Mock<ILicenseRepository>();
        var mockLocalApp = new Mock<ILocalDrivingLicenseApplicationService>();
        var mockDetained = new Mock<IDetainedLicenseRepository>();
        var mockClock = new Mock<IJordanClock>();

        var testToday = new DateOnly(2026, 11, 7);
        mockClock.SetupGet(c => c.Today).Returns(testToday);

        var expiredLicense = new License
        {
            LicenseID = 10,
            DriverID = 5,
            LicenseClass = 3,
            ExpirationDate = new DateTime(2025, 1, 1),
            IsActive = false
        };

        mockRepo.Setup(r => r.GetAllLicensesAsync(true, testToday, It.IsAny<CancellationToken>()))
            .ReturnsAsync([expiredLicense]);
        mockDetained.Setup(d => d.GetActiveDetainedLicenseIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mockRepo.Setup(r => r.GetNewerLicensesBatchAsync(It.IsAny<IEnumerable<License>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, License>
            {
                [10] = new License
                {
                    LicenseID = 20,
                    DriverID = 5,
                    LicenseClass = 3,
                    IssueReason = Domain.Enums.IssueReason.Renew
                }
            });

        var service = new LicenseQueryService(
            mockRepo.Object,
            mockLocalApp.Object,
            mockDetained.Object,
            mockClock.Object);

        // Act
        var result = await service.GetAllAsync(isExpired: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.False(result.Value![0].IsEligibleForRenewal);
        Assert.Contains("License #20", result.Value![0].IneligibilityReason);
        mockRepo.Verify(r => r.GetNewerLicensesBatchAsync(It.IsAny<IEnumerable<License>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void PagedResult_Map_PreservesMetadataAndProjectsItems()
    {
        var source = new Application.Common.Pagination.PagedResult<int>
        {
            Items = [1, 2, 3],
            PageNumber = 2,
            PageSize = 3,
            TotalCount = 10
        };

        var mapped = source.Map(x => x * 10);

        Assert.Equal([10, 20, 30], mapped.Items);
        Assert.Equal(2, mapped.PageNumber);
        Assert.Equal(3, mapped.PageSize);
        Assert.Equal(10, mapped.TotalCount);
        Assert.Equal(4, mapped.TotalPages);
        Assert.True(mapped.HasPreviousPage);
        Assert.True(mapped.HasNextPage);
    }

    [Fact]
    public void CurrentUserServiceExtensions_IsAuthenticated_RequiresLoggedInAndPositiveUserId()
    {
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        mockUser.SetupGet(u => u.UserId).Returns(1);
        Assert.True(mockUser.Object.IsAuthenticated());

        mockUser.SetupGet(u => u.UserId).Returns(0);
        Assert.False(mockUser.Object.IsAuthenticated());

        mockUser.SetupGet(u => u.UserId).Returns(1);
        mockUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        Assert.False(mockUser.Object.IsAuthenticated());
    }
}
