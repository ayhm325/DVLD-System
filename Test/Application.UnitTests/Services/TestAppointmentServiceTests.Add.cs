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
    // ADD - VALIDATION
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenDtoNull_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.AddAsync(null!);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Test appointment data is required.",
            result.Error);
    }

    [Fact]
    public async Task AddAsync_WhenTestTypeInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var dto = ValidCreateDto();
        dto.TestTypeID = 999;

        var result =
            await service.AddAsync(dto);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task AddAsync_WhenApplicationIdInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var dto = ValidCreateDto();
        dto.LocalDrivingLicenseApplicationID = 0;

        var result =
            await service.AddAsync(dto);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task AddAsync_WhenDateDefault_ReturnsValidation()
    {
        var service = CreateService();

        var dto = ValidCreateDto();
        dto.AppointmentDate = default;

        var result =
            await service.AddAsync(dto);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains(
            "Appointment date is required.",
            result.Error);
    }

    [Fact]
    public async Task AddAsync_WhenDateInPast_ReturnsValidation()
    {
        var service = CreateService();

        var dto = ValidCreateDto();
        dto.AppointmentDate =
            Common.FakeJordanClock.DefaultJordanNow.AddMinutes(-1);

        var result =
            await service.AddAsync(dto);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains(
            "Appointment date must be in the future.",
            result.Error);
    }

    [Fact]
    public async Task AddAsync_WhenRetakeIdInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var dto = ValidCreateDto();
        dto.RetakeTestApplicationID = 0;

        var result =
            await service.AddAsync(dto);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    // =========================================================
    // ADD - AUTH
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenNotLoggedIn_ReturnsForbidden()
    {
        var service = CreateService();

        _currentUserService
            .SetupGet(x => x.IsLoggedIn)
            .Returns(false);

        var result =
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal(
            "You must be logged in first.",
            result.Error);
    }

    [Fact]
    public async Task AddAsync_WhenUserIdInvalid_ReturnsForbidden()
    {
        var service = CreateService();

        _currentUserService
            .SetupGet(x => x.UserId)
            .Returns(0);

        var result =
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
    }

    // =========================================================
    // ADD - TEST TYPE
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenTestTypeNotFound_ReturnsNotFound()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestType?)null);

        var result =
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Test type not found.",
            result.Error);

        _unitOfWork.Verify(
            x =>
                x.BeginTransactionAsync(
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================
    // ADD - WORKFLOW
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenWorkflowRejects_ReturnsFailure()
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
                    "Test cannot be scheduled."));

        var result =
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "Test cannot be scheduled.",
            result.Error);

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<TestAppointment>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================
    // ADD - DUPLICATE
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenAlreadyScheduled_ReturnsConflict()
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
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "An appointment already exists for this test.",
            result.Error);
    }

    // =========================================================
    // ADD - SLOT OCCUPIED CONFLICT
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenSlotOccupied_ReturnsConflict()
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
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "This slot is already booked for this test type.",
            result.Error);
    }

    // =========================================================
    // ADD - APPLICANT SAME-DAY CONFLICT
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenApplicantHasSameDayConflict_ReturnsConflict()
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
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "The applicant already has an appointment or test scheduled on this calendar day.",
            result.Error);
    }

    // =========================================================
    // ADD - SUCCESS
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenValid_CreatesAndCommits()
    {
        var service = CreateService();

        SetupBasicAddFlow();

        TestAppointment? captured = null;

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Callback<TestAppointment, CancellationToken>(
                (entity, _) =>
                {
                    captured = entity;
                    entity.TestAppointmentID = 500;
                })
            .Returns(Task.CompletedTask);

        var result =
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);

        Assert.Equal(
            100,
            captured!.LocalDrivingLicenseApplicationID);

        Assert.Equal(
            1,
            captured.TestTypeID);

        Assert.Equal(
            10,
            captured.CreatedByUserID);

        Assert.Equal(
            25m,
            captured.PaidFees);

        Assert.False(captured.IsLocked);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transaction.Verify(
            x => x.CommitAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // ADD - SAVE FAILURE
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenSaveFails_ReturnsFailure()
    {
        var service = CreateService();

        SetupBasicAddFlow();

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Callback<TestAppointment, CancellationToken>(
                (x, _) => x.TestAppointmentID = 500)
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result =
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to book appointment.",
            result.Error);

        _transaction.Verify(
            x => x.CommitAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddAsync_WhenGeneratedIdIsInvalid_ReturnsFailure()
    {
        var service = CreateService();

        SetupBasicAddFlow();

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result =
            await service.AddAsync(
                ValidCreateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to book appointment.",
            result.Error);
    }

    // =========================================================
    // ADD - EXCEPTION
    // =========================================================

    [Fact]
    public async Task AddAsync_WhenExceptionOccurs_RollsBackAndRethrows()
    {
        var service = CreateService();

        SetupBasicAddFlow();

        _repository
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<TestAppointment>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Database error"));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddAsync(
                    ValidCreateDto()));

        Assert.Equal(
            "Database error",
            exception.Message);

        _transaction.Verify(
            x => x.RollbackAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transaction.Verify(
            x => x.CommitAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
