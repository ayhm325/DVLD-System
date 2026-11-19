using Application.Common.Results;
using Application.DTOs.ApplicationDTO;
using Application.DTOs.TestAppointmentDTO;
using Application.Mappers;
using Application.Validators;
using Domain.Enums;

namespace Application.Services;

public sealed partial class TestAppointmentService
{
    public async Task<Result<ScheduleTestDto>> GetSchedulePreparationAsync(
        int localAppId,
        int testTypeId,
        CancellationToken cancellationToken = default)
    {
        var appValidation = TestAppointmentValidator.ValidateApplicationId(localAppId);
        if (appValidation.IsFailure)
            return Result<ScheduleTestDto>.FromValidationFailure(appValidation.Error);

        var typeValidation = TestAppointmentValidator.ValidateTestTypeId(testTypeId);
        if (typeValidation.IsFailure)
            return Result<ScheduleTestDto>.FromValidationFailure(typeValidation.Error);

        var localResult =
            await _localApplicationService
                .GetLocalDrivingLicenseApplicationByIdAsync(localAppId, cancellationToken);

        if (localResult.IsFailure)
            return Result<ScheduleTestDto>.FromResult(localResult);

        var localApplication = localResult.Value;

        if (localApplication is null)
            return Result<ScheduleTestDto>.FromNotFound(
                "Local driving license application was not found.");

        var testType = await _testTypeRepository.GetByIdAsync(testTypeId, cancellationToken);

        if (testType is null)
            return Result<ScheduleTestDto>.FromNotFound("Test type not found.");

        var trial = await _repository.GetTrialCountAsync(localAppId, testTypeId, cancellationToken) + 1;
        var retakerFees = 0m;

        if (trial > 1)
        {
            var retakeResult =
                await _applicationTypeService
                    .GetApplicationTypeByIdAsync(RetakeApplicationTypeId, cancellationToken);

            if (retakeResult.IsFailure)
                return Result<ScheduleTestDto>.FromResult(retakeResult);

            retakerFees = retakeResult.Value?.ApplicationTypeFees ?? 0;
        }

        return Result<ScheduleTestDto>.Success(new ScheduleTestDto
        {
            AppointmentID = 0,
            RetakeTestApplicationID = null,
            LocalDrivingLicenseApplicationID = localAppId,
            LicenseClassName = localApplication.LicenseClassName,
            FullName = localApplication.FullName,
            Trial = trial,
            Date = _clock.Today.AddDays(1).ToDateTime(TimeOnly.MinValue),
            Fees = testType.TestTypeFees,
            TestTypeID = testTypeId,
            RetakerFees = retakerFees,
            TestID = 0,
            Result = false,
            Notes = null
        });
    }

    public async Task<Result> AddAsync(CreateTestAppointmentDto dto, CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateCreate(dto, _clock.Now);
        if (validation.IsFailure)
            return validation;

        if (!IsAuthenticated())
            return Result.Forbidden("You must be logged in first.");

        var testType = await _testTypeRepository.GetByIdAsync(dto.TestTypeID, cancellationToken);
        if (testType is null)
            return Result.NotFound("Test type not found.");

        return await ExecuteTransactionAsync(async transaction =>
        {
            if (!await _repository.LockLocalApplicationForSchedulingAsync(
                    dto.LocalDrivingLicenseApplicationID,
                    cancellationToken))
                return Result.NotFound(
                    "Local driving license application not found.");

            var workflow = await _workflowService.CanScheduleTestAsync(
                dto.LocalDrivingLicenseApplicationID,
                (TestTypeEnum)dto.TestTypeID,
                cancellationToken);

            if (workflow.IsFailure)
                return workflow;

            if (await _repository.IsAppointmentAlreadyScheduledAsync(
                    dto.LocalDrivingLicenseApplicationID,
                    dto.TestTypeID,
                    cancellationToken))
                return Result.Conflict(
                    "An appointment already exists for this test.");

            var applicantResult = await ResolveApplicantPersonIdAsync(
                dto.LocalDrivingLicenseApplicationID,
                cancellationToken);
            if (applicantResult.IsFailure)
                return Result.FromFailure(applicantResult);

            var slotCheck = await AcquireLocksAndValidateSlotAsync(
                applicantResult.Value,
                dto.TestTypeID,
                dto.AppointmentDate,
                excludeAppointmentId: null,
                cancellationToken);
            if (slotCheck.IsFailure)
                return slotCheck;

            var entity = TestAppointmentMapper.ToEntity(
                dto,
                testType.TestTypeFees,
                _currentUserService.UserId);

            await _repository.AddAsync(entity, cancellationToken);

            if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0 ||
                entity.TestAppointmentID <= 0)
                return Result.Failure("Failed to book appointment.");

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result<ScheduleTestDto>> ScheduleAsync(
        int localAppId,
        int testTypeId,
        DateTime appointmentDate,
        CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateSchedule(
            localAppId,
            testTypeId,
            appointmentDate,
            _clock.Now);

        if (validation.IsFailure)
            return Result<ScheduleTestDto>.FromValidationFailure(validation.Error);

        if (!IsAuthenticated())
            return Result<ScheduleTestDto>.FromForbidden(
                "You must be logged in first.");

        var testType = await _testTypeRepository.GetByIdAsync(testTypeId, cancellationToken);

        if (testType is null)
            return Result<ScheduleTestDto>.FromNotFound(
                "Test type not found.");

        var appointmentId = await ExecuteTransactionAsync(async transaction =>
        {
            if (!await _repository.LockLocalApplicationForSchedulingAsync(localAppId, cancellationToken))
                return Result<int>.FromNotFound(
                    "Local driving license application was not found.");

            var workflow = await _workflowService.CanScheduleTestAsync(
                localAppId,
                (TestTypeEnum)testTypeId,
                cancellationToken);

            if (workflow.IsFailure)
                return Result<int>.FromResult(workflow);

            if (await _repository.IsAppointmentAlreadyScheduledAsync(
                    localAppId,
                    testTypeId,
                    cancellationToken))
                return Result<int>.FromConflict(
                    "An appointment already exists for this test.");

            var applicantResult = await ResolveApplicantPersonIdAsync(
                localAppId,
                cancellationToken);
            if (applicantResult.IsFailure)
                return Result<int>.FromResult(applicantResult);

            var slotCheck = await AcquireLocksAndValidateSlotAsync(
                applicantResult.Value,
                testTypeId,
                appointmentDate,
                excludeAppointmentId: null,
                cancellationToken);
            if (slotCheck.IsFailure)
                return Result<int>.FromResult(slotCheck);

            var trial = await _repository.GetTrialCountAsync(
                localAppId,
                testTypeId,
                cancellationToken) + 1;

            int? retakeApplicationId = null;

            if (trial > 1)
            {
                var localResult =
                    await _localApplicationService
                        .GetLocalDrivingLicenseApplicationByIdAsync(localAppId, cancellationToken);

                if (localResult.IsFailure)
                    return Result<int>.FromResult(localResult);

                if (localResult.Value is null)
                    return Result<int>.FromNotFound(
                        "Local driving license application was not found.");

                var retakeResult =
                    await _applicationService.AddNewApplicationAsync(
                        new CreateApplicationDto
                        {
                            ApplicantPersonID = localResult.Value.ApplicantPersonID,
                            ApplicationTypeID = RetakeApplicationTypeId
                        },
                        cancellationToken);

                if (retakeResult.IsFailure)
                    return Result<int>.FromResult(retakeResult);

                retakeApplicationId = retakeResult.Value;
            }

            var appointment = TestAppointmentMapper.ToEntity(
                new CreateTestAppointmentDto
                {
                    TestTypeID = testTypeId,
                    LocalDrivingLicenseApplicationID = localAppId,
                    AppointmentDate = appointmentDate,
                    RetakeTestApplicationID = retakeApplicationId
                },
                testType.TestTypeFees,
                _currentUserService.UserId);

            await _repository.AddAsync(appointment, cancellationToken);

            if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0 ||
                appointment.TestAppointmentID <= 0)
                return Result<int>.FromFailure(
                    "Failed to book appointment.");

            await transaction.CommitAsync(cancellationToken);

            return Result<int>.Success(appointment.TestAppointmentID);
        }, cancellationToken);

        if (appointmentId.IsFailure)
            return Result<ScheduleTestDto>.FromResult(appointmentId);

        return await GetScheduleInfoAsync(appointmentId.Value, cancellationToken);
    }
}
