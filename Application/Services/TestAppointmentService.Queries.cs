using Application.Common.Pagination;
using Application.Common.Results;
using Application.DTOs.TestAppointmentDTO;
using Application.Mappers;
using Application.Validators;
using Domain.Enums;

namespace Application.Services;

public sealed partial class TestAppointmentService
{
    public async Task<Result<TestAppointmentDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateId(id);
        if (validation.IsFailure)
            return Result<TestAppointmentDto>.FromValidationFailure(validation.Error);

        var entity = await _repository.GetByIdAsync(id, cancellationToken);

        return entity is null
            ? Result<TestAppointmentDto>.FromNotFound("Appointment not found.")
            : Result<TestAppointmentDto>.Success(TestAppointmentMapper.ToDto(entity));
    }

    public async Task<Result<List<TestAppointmentDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return Result<List<TestAppointmentDto>>.Success(
            entities.Select(TestAppointmentMapper.ToDto).ToList());
    }

    public Task<Result<PagedResult<TestAppointmentDto>>> GetAllAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request is TestAppointmentQueryRequest queryRequest)
        {
            return GetAllAsync(queryRequest, cancellationToken);
        }

        return GetAllInternalAsync(() => _repository.GetAllAsync(request, cancellationToken));
    }

    public Task<Result<PagedResult<TestAppointmentDto>>> GetAllAsync(
        TestAppointmentQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.Equals(request.Tab?.Trim(), "today", StringComparison.OrdinalIgnoreCase) &&
            !request.TodayDate.HasValue)
        {
            request.TodayDate = _clock.Today;
        }

        return GetAllInternalAsync(() => _repository.GetAllAsync(request, cancellationToken));
    }

    private async Task<Result<PagedResult<TestAppointmentDto>>> GetAllInternalAsync(
        Func<Task<PagedResult<Domain.Entities.TestAppointment>>> fetcher)
    {
        var paged = await fetcher();
        var items = paged.Items.Select(TestAppointmentMapper.ToDto).ToList();

        return Result<PagedResult<TestAppointmentDto>>.Success(
            new PagedResult<TestAppointmentDto>
            {
                Items = items,
                PageNumber = paged.PageNumber,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount
            });
    }

    public async Task<Result<List<TestAppointmentDto>>> GetByLocalDrivingLicenseApplicationIdAsync(int localAppId, CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateApplicationId(localAppId);
        if (validation.IsFailure)
            return Result<List<TestAppointmentDto>>.FromValidationFailure(validation.Error);

        var entities = await _repository.GetByLocalDrivingLicenseApplicationIdAsync(localAppId, cancellationToken);

        return Result<List<TestAppointmentDto>>.Success(
            entities.Select(TestAppointmentMapper.ToDto).ToList());
    }

    public async Task<Result<List<TestAppointmentDto>>> GetByTestTypeIdAsync(int testType, CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateTestTypeId((int)testType);
        if (validation.IsFailure)
            return Result<List<TestAppointmentDto>>.FromValidationFailure(validation.Error);

        var entities = await _repository.GetByTestTypeIdAsync((TestTypeEnum)testType, cancellationToken);

        return Result<List<TestAppointmentDto>>.Success(
            entities.Select(TestAppointmentMapper.ToDto).ToList());
    }

    public async Task<Result<List<TestAppointmentDto>>> GetByCreatedUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateUserId(userId);
        if (validation.IsFailure)
            return Result<List<TestAppointmentDto>>.FromValidationFailure(validation.Error);

        var entities = await _repository.GetByCreatedUserIdAsync(userId, cancellationToken);

        return Result<List<TestAppointmentDto>>.Success(
            entities.Select(TestAppointmentMapper.ToDto).ToList());
    }

    public async Task<Result<ScheduleTestDto>> GetScheduleInfoAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateId(appointmentId);
        if (validation.IsFailure)
            return Result<ScheduleTestDto>.FromValidationFailure(validation.Error);

        var entity = await _repository.GetScheduleInfoAsync(appointmentId, cancellationToken);
        if (entity is null)
            return Result<ScheduleTestDto>.FromNotFound("Appointment data not found.");

        var priorAttempts = await _repository.GetTrialCountAsync(
            entity.LocalDrivingLicenseApplicationID,
            entity.TestTypeID,
            cancellationToken);

        var isRecordedAttempt = entity.Status is AppointmentStatus.Completed or AppointmentStatus.NoShow || entity.Test is not null;
        var trial = isRecordedAttempt ? Math.Max(1, priorAttempts) : priorAttempts + 1;

        return Result<ScheduleTestDto>.Success(
            TestAppointmentMapper.ToScheduleDto(entity, trial));
    }

    public async Task<int> GetTrialCountAsync(int localAppId, int testTypeId, CancellationToken cancellationToken = default) =>
        TestAppointmentValidator.ValidateApplicationId(localAppId).IsFailure ||
        TestAppointmentValidator.ValidateTestTypeId(testTypeId).IsFailure
            ? 0
            : await _repository.GetTrialCountAsync(localAppId, testTypeId, cancellationToken);

    public async Task<decimal> GetTestTypeFeesAsync(int testTypeId, CancellationToken cancellationToken = default)
    {
        if (TestAppointmentValidator.ValidateTestTypeId(testTypeId).IsFailure)
            return 0;

        var type = await _testTypeRepository.GetByIdAsync(testTypeId, cancellationToken);
        return type?.TestTypeFees ?? 0;
    }

    public Task<bool> IsAppointmentAlreadyScheduledAsync(
        int localAppId,
        int testTypeId,
        CancellationToken cancellationToken = default) =>
        _repository.IsAppointmentAlreadyScheduledAsync(
            localAppId,
            testTypeId,
            cancellationToken);

    public Task<ExaminationAttemptStats> GetAttemptStatsAsync(
        int localAppId,
        int testTypeId,
        CancellationToken cancellationToken = default) =>
        _repository.GetAttemptStatsAsync(localAppId, testTypeId, cancellationToken);
}
