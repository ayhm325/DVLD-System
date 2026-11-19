using Application.Common.Results;
using Application.DTOs.ApplicationDTO;
using Application.DTOs.LocalDrivingLicenseApplicationDTO;
using Application.DTOs.TestAppointmentDTO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data;

namespace Application.UnitTests.Services;

public sealed partial class TestAppointmentServiceTests
{
    // =========================================================
    // SCHEDULE - VALIDATION
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenApplicationIdInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.ScheduleAsync(
                0,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task ScheduleAsync_WhenTestTypeInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.ScheduleAsync(
                100,
                999,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task ScheduleAsync_WhenDateInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.ScheduleAsync(
                100,
                1,
                Common.FakeJordanClock.DefaultJordanNow.AddMinutes(-1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    // =========================================================
    // SCHEDULE - AUTH
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenNotAuthenticated_ReturnsForbidden()
    {
        var service = CreateService();

        _currentUserService
            .SetupGet(x => x.IsLoggedIn)
            .Returns(false);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal(
            "You must be logged in first.",
            result.Error);
    }

    // =========================================================
    // SCHEDULE - TEST TYPE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenTestTypeNotFound_ReturnsNotFound()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestType?)null);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Test type not found.",
            result.Error);
    }

    // =========================================================
    // SCHEDULE - WORKFLOW
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenWorkflowRejects_RollsBack()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestType());

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Conflict(
                    "Workflow rejected."));

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);

        _transaction.Verify(
            x => x.RollbackAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - DUPLICATE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenAlreadyScheduled_RollsBack()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestType());

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.IsAppointmentAlreadyScheduledAsync(
                    100,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);

        _transaction.Verify(
            x => x.RollbackAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - SLOT OCCUPIED CONFLICT
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenSlotOccupied_RollsBack()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestType());

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.LockLocalApplicationForSchedulingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    5,
                    1,
                    It.IsAny<DateTime>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.IsAppointmentAlreadyScheduledAsync(
                    100,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    1,
                    It.IsAny<DateTime>(),
                    null,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "This slot is already booked for this test type.",
            result.Error);

        _transaction.Verify(
            x => x.RollbackAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - APPLICANT SAME-DAY CONFLICT
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenApplicantHasSameDayConflict_RollsBack()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestType());

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.LockLocalApplicationForSchedulingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    5,
                    1,
                    It.IsAny<DateTime>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.IsAppointmentAlreadyScheduledAsync(
                    100,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    1,
                    It.IsAny<DateTime>(),
                    null,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.HasApplicantSameDayConflictAsync(
                    5,
                    It.IsAny<DateTime>(),
                    null,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "The applicant already has an appointment or test scheduled on this calendar day.",
            result.Error);

        _transaction.Verify(
            x => x.RollbackAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - FIRST ATTEMPT
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_FirstAttempt_DoesNotCreateRetakeApplication()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        TestAppointment? captured = null;

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Callback<TestAppointment, CancellationToken>(
                (x, _) =>
                {
                    captured = x;
                    x.TestAppointmentID = 700;
                })
            .Returns(Task.CompletedTask);

        var appointmentDate =
            ValidFutureSlot(2);

        var scheduleInfo =
            ValidAppointment(
                700,
                10,
                false,
                appointmentDate);

        _repository
            .Setup(x =>
                x.GetScheduleInfoAsync(700, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduleInfo);

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                appointmentDate);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Null(
            captured!.RetakeTestApplicationID);

        _applicationService.Verify(
            x =>
                x.GetBasicInfoAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _applicationService.Verify(
            x =>
                x.AddNewApplicationAsync(
                    It.IsAny<CreateApplicationDto>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _transaction.Verify(
            x =>
                x.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - RETAKE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_Retake_CreatesRetakeApplication()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _localApplicationService
            .Setup(x =>
                x.GetLocalDrivingLicenseApplicationByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalDrivingLicenseApplicationListDto>
                    .Success(
                        new LocalDrivingLicenseApplicationListDto
                        {
                            ApplicantPersonID = 55,
                            LocalDrivingLicenseApplicationID = 100,
                            LicenseClassID = 1,
                            LicenseClassName = "A",
                            FullName = "Test Person",
                            ApplicationDate = Common.FakeJordanClock.DefaultJordanNow,
                            PassedTest = 0,
                            ApplicationStatus = AppStatus.New,
                            ApplicationFees = 50m,
                            LicenseClassFees = 100m,
                            HasLicense = false
                        }));

        _applicationService
            .Setup(x =>
                x.AddNewApplicationAsync(
                    It.IsAny<CreateApplicationDto>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<int>.Success(900));

        TestAppointment? captured = null;

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Callback<TestAppointment, CancellationToken>(
                (x, _) =>
                {
                    captured = x;
                    x.TestAppointmentID = 800;
                })
            .Returns(Task.CompletedTask);

        var date =
            ValidFutureSlot(2);

        _repository
            .Setup(x =>
                x.GetScheduleInfoAsync(800, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ValidAppointment(
                    800,
                    10,
                    false,
                    date));

        var result =
            await service.ScheduleAsync(
                100,
                1,
                date);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);

        Assert.Equal(
            900,
            captured!.RetakeTestApplicationID);

        _applicationService.Verify(
            x =>
                x.AddNewApplicationAsync(
                    It.Is<CreateApplicationDto>(
                        dto =>
                            dto.ApplicantPersonID == 55 &&
                            dto.ApplicationTypeID == 7),
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transaction.Verify(
            x =>
                x.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - RETAKE LOCAL APPLICATION FAILURE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_Retake_WhenLocalApplicationFails_RollsBack()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _localApplicationService
            .Setup(x =>
                x.GetLocalDrivingLicenseApplicationByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalDrivingLicenseApplicationListDto>
                    .FromNotFound(
                        "Local application not found."));

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorType.NotFound,
            result.ErrorType);

        _transaction.Verify(
            x =>
                x.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _applicationService.Verify(
            x =>
                x.AddNewApplicationAsync(
                    It.IsAny<CreateApplicationDto>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================
    // SCHEDULE - RETAKE LOCAL APPLICATION NULL
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_Retake_WhenLocalApplicationValueNull_ReturnsNotFound()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _localApplicationService
            .Setup(x =>
                x.GetLocalDrivingLicenseApplicationByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalDrivingLicenseApplicationListDto>
                    .Success(null!));

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorType.NotFound,
            result.ErrorType);
        Assert.Equal(
            "Local driving license application was not found.",
            result.Error);

        _transaction.Verify(
            x =>
                x.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - RETAKE APPLICANT SOURCE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_Retake_UsesLocalApplicationApplicantPersonId()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _localApplicationService
            .Setup(x =>
                x.GetLocalDrivingLicenseApplicationByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalDrivingLicenseApplicationListDto>
                    .Success(
                        new LocalDrivingLicenseApplicationListDto
                        {
                            ApplicantPersonID = 55,
                            LocalDrivingLicenseApplicationID = 100,
                            LicenseClassID = 1,
                            LicenseClassName = "A",
                            FullName = "Test Person",
                            ApplicationDate = Common.FakeJordanClock.DefaultJordanNow,
                            PassedTest = 0,
                            ApplicationStatus = AppStatus.New,
                            ApplicationFees = 50m,
                            LicenseClassFees = 100m,
                            HasLicense = false
                        }));

        _applicationService
            .Setup(x =>
                x.AddNewApplicationAsync(
                    It.IsAny<CreateApplicationDto>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<int>.Success(900));

        TestAppointment? captured = null;

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Callback<TestAppointment, CancellationToken>(
                (x, _) =>
                {
                    captured = x;
                    x.TestAppointmentID = 800;
                })
            .Returns(Task.CompletedTask);

        var date =
            ValidFutureSlot(2);

        _repository
            .Setup(x =>
                x.GetScheduleInfoAsync(800, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ValidAppointment(
                    800,
                    10,
                    false,
                    date));

        var result =
            await service.ScheduleAsync(
                100,
                1,
                date);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);

        _applicationService.Verify(
            x =>
                x.AddNewApplicationAsync(
                    It.Is<CreateApplicationDto>(
                        dto =>
                            dto.ApplicantPersonID == 55 &&
                            dto.ApplicationTypeID == 7),
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - RETAKE APPLICATION FAILURE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_Retake_WhenCreateApplicationFails_RollsBack()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _localApplicationService
            .Setup(x =>
                x.GetLocalDrivingLicenseApplicationByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalDrivingLicenseApplicationListDto>
                    .Success(
                        new LocalDrivingLicenseApplicationListDto
                        {
                            ApplicantPersonID = 55,
                            LocalDrivingLicenseApplicationID = 100,
                            LicenseClassID = 1,
                            LicenseClassName = "A",
                            FullName = "Test Person",
                            ApplicationDate = Common.FakeJordanClock.DefaultJordanNow,
                            PassedTest = 0,
                            ApplicationStatus = AppStatus.New,
                            ApplicationFees = 50m,
                            LicenseClassFees = 100m,
                            HasLicense = false
                        }));

        _applicationService
            .Setup(x =>
                x.AddNewApplicationAsync(
                    It.IsAny<CreateApplicationDto>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<int>.FromFailure(
                    "Failed to create retake application."));

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorType.Failure,
            result.ErrorType);

        _transaction.Verify(
            x =>
                x.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _repository.Verify(
            x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================
    // SCHEDULE - SAVE FAILURE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenSaveFails_RollsBack()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Callback<TestAppointment, CancellationToken>(
                (x, _) => x.TestAppointmentID = 700)
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.ErrorType);

        _transaction.Verify(
            x =>
                x.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transaction.Verify(
            x =>
                x.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================
    // SCHEDULE - GENERATED ID FAILURE
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenGeneratedIdInvalid_RollsBack()
    {
        var service = CreateService();

        SetupBasicScheduleFlow();

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result =
            await service.ScheduleAsync(
                100,
                1,
                ValidFutureSlot(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.ErrorType);

        _transaction.Verify(
            x =>
                x.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // SCHEDULE - EXCEPTION
    // =========================================================

    [Fact]
    public async Task ScheduleAsync_WhenExceptionOccurs_RollsBackAndRethrows()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestType());

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.IsAppointmentAlreadyScheduledAsync(
                    100,
                    1,
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Database error"));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ScheduleAsync(
                    100,
                    1,
                    ValidFutureSlot(2)));

        Assert.Equal(
            "Database error",
            exception.Message);

        _transaction.Verify(
            x =>
                x.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transaction.Verify(
            x =>
                x.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
