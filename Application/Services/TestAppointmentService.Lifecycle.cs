using Application.Common.Results;
using Application.DTOs.TestAppointmentDTO;
using Application.Validators;
using Domain.Enums;

namespace Application.Services;

public sealed partial class TestAppointmentService
{
    public async Task<Result> UpdateAsync(UpdateTestAppointmentDto dto, CancellationToken cancellationToken = default)
    {
        var now = _clock.Now;
        var validation = TestAppointmentValidator.ValidateUpdate(dto, now);
        if (validation.IsFailure)
            return validation;

        if (!IsAuthenticated())
            return Result.Forbidden("You must be logged in first.");

        return await ExecuteTransactionAsync(async transaction =>
        {
            var entity = await _repository.GetForUpdateAsync(
                dto.TestAppointmentID,
                cancellationToken);

            if (entity is null)
                return Result.NotFound("Appointment not found.");

            if (entity.Status == AppointmentStatus.Cancelled)
                return Result.Conflict("Cannot modify a cancelled appointment.");

            if (entity.Status == AppointmentStatus.Completed || entity.Test is not null)
                return Result.Conflict("Cannot modify a completed appointment.");

            if (entity.Status == AppointmentStatus.NoShow)
                return Result.Conflict("Cannot modify a no-show appointment.");

            if (entity.Status == null)
                return Result.Conflict("Appointment has an unresolved lifecycle status and cannot be modified.");

            if (entity.IsLocked)
                return Result.Conflict(
                    "Cannot modify a locked appointment.");

            var cutoff = TestAppointmentValidator.ValidateModificationOrCancellationCutoff(
                entity.AppointmentDate,
                _clock.Now);
            if (cutoff.IsFailure)
                return cutoff;

            if (entity.AppointmentDate == dto.AppointmentDate)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            }

            var workflow = await _workflowService.CanScheduleTestAsync(
                entity.LocalDrivingLicenseApplicationID,
                (TestTypeEnum)entity.TestTypeID,
                cancellationToken);

            if (workflow.IsFailure)
                return workflow;

            var applicantResult = await ResolveApplicantPersonIdAsync(
                entity.LocalDrivingLicenseApplicationID,
                cancellationToken);
            if (applicantResult.IsFailure)
                return Result.FromFailure(applicantResult);

            var slotCheck = await AcquireLocksAndValidateSlotAsync(
                applicantResult.Value,
                entity.TestTypeID,
                dto.AppointmentDate,
                entity.TestAppointmentID,
                cancellationToken);
            if (slotCheck.IsFailure)
                return slotCheck;

            entity.AppointmentDate = dto.AppointmentDate;

            if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
                return Result.Failure("Failed to update appointment.");

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateId(id);
        if (validation.IsFailure)
            return validation;

        if (!IsAuthenticated())
            return Result.Forbidden("You must be logged in first.");

        return await ExecuteTransactionAsync(async transaction =>
        {
            var entity = await _repository.GetForUpdateAsync(id, cancellationToken);

            if (entity is null)
                return Result.NotFound("Appointment not found.");

            if (entity.Status == AppointmentStatus.Cancelled)
                return Result.Conflict("Appointment is already cancelled.");

            if (entity.Status == AppointmentStatus.Completed || entity.Test is not null)
                return Result.Conflict("Cannot cancel a completed appointment.");

            if (entity.Status == AppointmentStatus.NoShow)
                return Result.Conflict("Cannot cancel a no-show appointment.");

            if (entity.Status == null)
                return Result.Conflict("Appointment has an unresolved lifecycle status and cannot be cancelled.");

            if (entity.IsLocked)
                return Result.Conflict("Cannot cancel a locked appointment.");

            var nowJordan = _clock.Now;
            var cutoff = TestAppointmentValidator.ValidateModificationOrCancellationCutoff(
                entity.AppointmentDate,
                nowJordan);
            if (cutoff.IsFailure)
                return cutoff;

            entity.Status = AppointmentStatus.Cancelled;
            entity.CancelledAt = nowJordan;
            entity.CancelledByUserID = _currentUserService.UserId;
            entity.IsLocked = true;

            if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
                return Result.Failure("Failed to cancel appointment.");

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default) => CancelAsync(id, cancellationToken);
}
