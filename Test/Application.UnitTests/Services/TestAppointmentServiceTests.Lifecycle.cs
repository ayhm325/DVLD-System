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
    // UPDATE - VALIDATION
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenDtoNull_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.UpdateAsync(null!);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAsync_WhenIdInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.UpdateAsync(
                ValidUpdateDto(0));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAsync_WhenDateInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var dto = ValidUpdateDto();
        dto.AppointmentDate = default;

        var result =
            await service.UpdateAsync(dto);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    // =========================================================
    // UPDATE - AUTH
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenNotAuthenticated_ReturnsForbidden()
    {
        var service = CreateService();

        _currentUserService
            .SetupGet(x => x.IsLoggedIn)
            .Returns(false);

        var result =
            await service.UpdateAsync(
                ValidUpdateDto());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal(
            "You must be logged in first.",
            result.Error);
    }

    // =========================================================
    // UPDATE - NOT FOUND
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsNotFound()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (TestAppointment?)null);

        var result =
            await service.UpdateAsync(
                ValidUpdateDto(1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Appointment not found.",
            result.Error);
    }

    // =========================================================
    // UPDATE - STAFF EQUALITY
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenAnotherUserCreatedAppointment_CanUpdateSuccessfully()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1, 99); // Created by user 99, current user is 10

        var newDate =
            ValidFutureSlot(5, 10, 0);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    5,
                    1,
                    newDate,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    1,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.HasApplicantSameDayConflictAsync(
                    5,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result =
            await service.UpdateAsync(
                new UpdateTestAppointmentDto
                {
                    TestAppointmentID = 1,
                    AppointmentDate = newDate
                });

        Assert.True(result.IsSuccess);
        Assert.Equal(newDate, appointment.AppointmentDate);
        Assert.Equal(99, appointment.CreatedByUserID);
    }

    // =========================================================
    // UPDATE - LOCKED
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenLocked_ReturnsConflict()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ValidAppointment(
                    1,
                    10,
                    true));

        var result =
            await service.UpdateAsync(
                ValidUpdateDto(1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "Cannot modify a locked appointment.",
            result.Error);
    }

    // =========================================================
    // UPDATE - SAME DATE
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenDateUnchanged_CommitsWithoutSave()
    {
        var service = CreateService();

        var date =
            ValidFutureSlot(4);

        var appointment =
            ValidAppointment(
                1,
                10,
                false,
                date);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var dto = new UpdateTestAppointmentDto
        {
            TestAppointmentID = 1,
            AppointmentDate = date
        };

        var result =
            await service.UpdateAsync(dto);

        Assert.True(result.IsSuccess);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        _transaction.Verify(
            x => x.CommitAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // UPDATE - WORKFLOW
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenWorkflowRejects_ReturnsFailure()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

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
            await service.UpdateAsync(
                ValidUpdateDto(1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    // =========================================================
    // UPDATE - SLOT OCCUPIED CONFLICT
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenSlotOccupied_ReturnsConflict()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1);

        var newDate =
            ValidFutureSlot(5, 10, 0);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    5,
                    1,
                    newDate,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    1,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result =
            await service.UpdateAsync(
                new UpdateTestAppointmentDto
                {
                    TestAppointmentID = 1,
                    AppointmentDate = newDate
                });

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "This slot is already booked for this test type.",
            result.Error);
    }

    // =========================================================
    // UPDATE - APPLICANT SAME-DAY CONFLICT
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenApplicantHasSameDayConflict_ReturnsConflict()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1);

        var newDate =
            ValidFutureSlot(5, 10, 0);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    5,
                    1,
                    newDate,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    1,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.HasApplicantSameDayConflictAsync(
                    5,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result =
            await service.UpdateAsync(
                new UpdateTestAppointmentDto
                {
                    TestAppointmentID = 1,
                    AppointmentDate = newDate
                });

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "The applicant already has an appointment or test scheduled on this calendar day.",
            result.Error);
    }

    // =========================================================
    // UPDATE - SUCCESS
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenValid_UpdatesAndCommits()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1);

        var newDate =
            ValidFutureSlot(5, 10, 0);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    5,
                    1,
                    newDate,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    1,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.HasApplicantSameDayConflictAsync(
                    5,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result =
            await service.UpdateAsync(
                new UpdateTestAppointmentDto
                {
                    TestAppointmentID = 1,
                    AppointmentDate = newDate
                });

        Assert.True(result.IsSuccess);
        Assert.Equal(
            newDate,
            appointment.AppointmentDate);

        _unitOfWork.Verify(
            x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transaction.Verify(
            x =>
                x.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // UPDATE - SAVE FAILURE
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenSaveFails_ReturnsFailure()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1);

        var newDate =
            ValidFutureSlot(5, 10, 0);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    5,
                    1,
                    newDate,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    1,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.HasApplicantSameDayConflictAsync(
                    5,
                    newDate,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result =
            await service.UpdateAsync(
                new UpdateTestAppointmentDto
                {
                    TestAppointmentID = 1,
                    AppointmentDate = newDate
                });

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to update appointment.",
            result.Error);
    }

    // =========================================================
    // UPDATE - EXCEPTION
    // =========================================================

    [Fact]
    public async Task UpdateAsync_WhenExceptionOccurs_RollsBackAndRethrows()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Database error"));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(
                    ValidUpdateDto(1)));

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

    // =========================================================
    // DELETE - VALIDATION
    // =========================================================

    [Fact]
    public async Task DeleteAsync_WhenIdInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.DeleteAsync(0);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    // =========================================================
    // DELETE - AUTH
    // =========================================================

    [Fact]
    public async Task DeleteAsync_WhenNotAuthenticated_ReturnsForbidden()
    {
        var service = CreateService();

        _currentUserService
            .SetupGet(x => x.IsLoggedIn)
            .Returns(false);

        var result =
            await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal(
            "You must be logged in first.",
            result.Error);
    }

    // =========================================================
    // DELETE - NOT FOUND
    // =========================================================

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsNotFound()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (TestAppointment?)null);

        var result =
            await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Appointment not found.",
            result.Error);
    }

    // =========================================================
    // DELETE - STAFF EQUALITY
    // =========================================================

    [Fact]
    public async Task DeleteAsync_WhenAnotherUserCreatedAppointment_CanCancelSuccessfully()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1, 99); // Created by user 99, current user is 10

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result =
            await service.DeleteAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.True(appointment.IsLocked);
        Assert.NotNull(appointment.CancelledAt);
        Assert.Equal(10, appointment.CancelledByUserID);
        _repository.Verify(
            x => x.Delete(It.IsAny<TestAppointment>()),
            Times.Never);
    }

    // =========================================================
    // DELETE / CANCEL - LOCKED
    // =========================================================

    [Fact]
    public async Task DeleteAsync_WhenLocked_ReturnsConflict()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ValidAppointment(
                    1,
                    10,
                    true));

        var result =
            await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "Cannot cancel a locked appointment.",
            result.Error);
    }

    // =========================================================
    // DELETE / CANCEL - SUCCESS (SOFT CANCELLATION)
    // =========================================================

    [Fact]
    public async Task DeleteAsync_WhenValid_SoftCancelsAndSaves()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var result =
            await service.DeleteAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.True(appointment.IsLocked);
        Assert.NotNull(appointment.CancelledAt);
        Assert.Equal(10, appointment.CancelledByUserID);

        _repository.Verify(
            x => x.Delete(It.IsAny<TestAppointment>()),
            Times.Never);

        _unitOfWork.Verify(
            x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // DELETE / CANCEL - SAVE FAILURE
    // =========================================================

    [Fact]
    public async Task DeleteAsync_WhenSaveFails_ReturnsFailure()
    {
        var service = CreateService();

        var appointment =
            ValidAppointment(1);

        _repository
            .Setup(x =>
                x.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result =
            await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to cancel appointment.",
            result.Error);

        _repository.Verify(
            x => x.Delete(It.IsAny<TestAppointment>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelAsync_WhenAlreadyCancelled_ReturnsConflict()
    {
        var service = CreateService();
        var appointment = ValidAppointment(1);
        appointment.Status = AppointmentStatus.Cancelled;

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.CancelAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Appointment is already cancelled.", result.Error);
    }

    [Fact]
    public async Task CancelAsync_WhenCompleted_ReturnsConflict()
    {
        var service = CreateService();
        var appointment = ValidAppointment(1);
        appointment.Status = AppointmentStatus.Completed;

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.CancelAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Cannot cancel a completed appointment.", result.Error);
    }

    [Fact]
    public async Task CancelAsync_WhenNoShow_ReturnsConflict()
    {
        var service = CreateService();
        var appointment = ValidAppointment(1);
        appointment.Status = AppointmentStatus.NoShow;

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.CancelAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Cannot cancel a no-show appointment.", result.Error);
    }

    [Fact]
    public async Task CancelAsync_WhenStatusNull_ReturnsConflict()
    {
        var service = CreateService();
        var appointment = ValidAppointment(1);
        appointment.Status = null;

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.CancelAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Appointment has an unresolved lifecycle status and cannot be cancelled.", result.Error);
    }

    [Fact]
    public async Task CancelAsync_WhenCutoffViolated_ReturnsValidationFailure()
    {
        var service = CreateService();
        // Appointment is tomorrow (< 2 calendar days away)
        var jordanNow = _clock.Now;
        var appointment = ValidAppointment(1, date: jordanNow.Date.AddDays(1).AddHours(9));

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.CancelAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Appointments cannot be modified or cancelled less than two calendar days before the scheduled date.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenCutoffViolated_ReturnsValidationFailure()
    {
        var service = CreateService();
        // Existing appointment is tomorrow (< 2 calendar days away)
        var jordanNow = _clock.Now;
        var appointment = ValidAppointment(1, date: jordanNow.Date.AddDays(1).AddHours(9));

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.UpdateAsync(ValidUpdateDto(1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Appointments cannot be modified or cancelled less than two calendar days before the scheduled date.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenCancelled_ReturnsConflict()
    {
        var service = CreateService();
        var appointment = ValidAppointment(1);
        appointment.Status = AppointmentStatus.Cancelled;

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.UpdateAsync(ValidUpdateDto(1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Cannot modify a cancelled appointment.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenStatusNull_ReturnsConflict()
    {
        var service = CreateService();
        var appointment = ValidAppointment(1);
        appointment.Status = null;

        _repository.Setup(x => x.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var result = await service.UpdateAsync(ValidUpdateDto(1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Appointment has an unresolved lifecycle status and cannot be modified.", result.Error);
    }
}
