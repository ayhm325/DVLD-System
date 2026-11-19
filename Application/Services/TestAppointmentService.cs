using Application.Common.Results;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Application.Services;

public sealed partial class TestAppointmentService(
    IUnitOfWork unitOfWork,
    ITestAppointmentRepository repository,
    ITestTypeRepository testTypeRepository,
    ILocalDrivingLicenseApplicationService localApplicationService,
    IApplicationTypeService applicationTypeService,
    ICurrentUserService currentUserService,
    ITestWorkflowService workflowService,
    IApplicationService applicationService,
    ILogger<TestAppointmentService> logger,
    IJordanClock clock) : ITestAppointmentService
{
    private const int RetakeApplicationTypeId = 7;
    private const int SchedulingLockTimeoutMs = 5000;

    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly ITestAppointmentRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ITestTypeRepository _testTypeRepository = testTypeRepository ?? throw new ArgumentNullException(nameof(testTypeRepository));
    private readonly ILocalDrivingLicenseApplicationService _localApplicationService = localApplicationService ?? throw new ArgumentNullException(nameof(localApplicationService));
    private readonly IApplicationTypeService _applicationTypeService = applicationTypeService ?? throw new ArgumentNullException(nameof(applicationTypeService));
    private readonly ICurrentUserService _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    private readonly ITestWorkflowService _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
    private readonly IApplicationService _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
    private readonly ILogger<TestAppointmentService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IJordanClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    private bool IsAuthenticated() =>
        _currentUserService.IsLoggedIn && _currentUserService.UserId > 0;

    private async Task<T> ExecuteTransactionAsync<T>(
        Func<IUnitOfWorkTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default) =>
        await _unitOfWork.ExecuteInTransactionAsync(
            operation,
            IsolationLevel.ReadCommitted,
            cancellationToken);

    private async Task<Result<int>> ResolveApplicantPersonIdAsync(
        int localAppId,
        CancellationToken cancellationToken)
    {
        var applicantPersonId = await _repository.GetApplicantPersonIdByLocalAppIdAsync(
            localAppId,
            cancellationToken);

        if (!applicantPersonId.HasValue)
        {
            var localApp = await _localApplicationService.GetLocalDrivingLicenseApplicationByIdAsync(
                localAppId,
                cancellationToken);
            if (localApp.IsSuccess && localApp.Value is not null)
            {
                applicantPersonId = localApp.Value.ApplicantPersonID;
            }
        }

        return applicantPersonId.HasValue
            ? Result<int>.Success(applicantPersonId.Value)
            : Result<int>.FromNotFound("Applicant person not found.");
    }

    private async Task<Result> AcquireLocksAndValidateSlotAsync(
        int applicantPersonId,
        int testTypeId,
        DateTime appointmentDate,
        int? excludeAppointmentId,
        CancellationToken cancellationToken)
    {
        if (!await _repository.AcquireSchedulingLocksAsync(
                applicantPersonId,
                testTypeId,
                appointmentDate,
                SchedulingLockTimeoutMs,
                cancellationToken))
        {
            return Result.Conflict(
                "Unable to acquire scheduling lock for the selected slot. Please try again.");
        }

        if (await _repository.IsSlotOccupiedAsync(
                testTypeId,
                appointmentDate,
                excludeAppointmentId,
                cancellationToken))
        {
            return Result.Conflict(
                "This slot is already booked for this test type.");
        }

        if (await _repository.HasApplicantSameDayConflictAsync(
                applicantPersonId,
                appointmentDate,
                excludeAppointmentId,
                cancellationToken))
        {
            return Result.Conflict(
                "The applicant already has an appointment or test scheduled on this calendar day.");
        }

        return Result.Success();
    }
}
