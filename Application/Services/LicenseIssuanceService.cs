using System.Data;
using Application.Common.Results;
using Application.DTOs;
using Application.DTOs.LicenseDTO;
using Application.Interfaces;
using Application.Mappers;
using Application.Validators;
using Domain.Enums;

namespace Application.Services;

public sealed class LicenseIssuanceService(
    IUnitOfWork unitOfWork,
    ILicenseRepository licenseRepository,
    ILocalDrivingLicenseApplicationService localApplicationService,
    IApplicationService applicationService,
    IDriverService driverService,
    IPersonService personService,
    ICurrentUserService currentUserService,
    ILicenseClassService licenseClassService,
    ITestWorkflowService testWorkflowService,
    IJordanClock clock) : ILicenseIssuanceService
{
    private const int NewLocalDrivingLicenseApplicationTypeId = 1;

    private readonly IUnitOfWork _unitOfWork =
        unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly ILicenseRepository _licenseRepository =
        licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
    private readonly ILocalDrivingLicenseApplicationService _localApplicationService =
        localApplicationService ?? throw new ArgumentNullException(nameof(localApplicationService));
    private readonly IApplicationService _applicationService =
        applicationService ?? throw new ArgumentNullException(nameof(applicationService));
    private readonly IDriverService _driverService =
        driverService ?? throw new ArgumentNullException(nameof(driverService));
    private readonly IPersonService _personService =
        personService ?? throw new ArgumentNullException(nameof(personService));
    private readonly ICurrentUserService _currentUserService =
        currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    private readonly ILicenseClassService _licenseClassService =
        licenseClassService ?? throw new ArgumentNullException(nameof(licenseClassService));
    private readonly ITestWorkflowService _testWorkflowService =
        testWorkflowService ?? throw new ArgumentNullException(nameof(testWorkflowService));
    private readonly IJordanClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<Result<int>> IssueFirstLicenseAsync(int localAppId, string? notes, CancellationToken cancellationToken = default)
    {
        var validation = LicenseValidator.ValidateId(localAppId);
        if (validation.IsFailure)
            return Result<int>.FromValidationFailure(validation.Error);

        if (!_currentUserService.IsAuthenticated())
            return Result<int>.FromForbidden("Authenticated user is required.");

        var localResult = await _localApplicationService
            .GetLocalDrivingLicenseApplicationByIdAsync(localAppId, cancellationToken);

        if (localResult.IsFailure)
            return Result<int>.FromResult(localResult);

        if (localResult.Value is null)
            return Result<int>.FromNotFound(
                "Local driving license application was not found.");

        var localApplication = localResult.Value;

        var applicationIdResult =
            await _localApplicationService.GetApplicationIdByLocalIdAsync(localAppId, cancellationToken);

        if (applicationIdResult.IsFailure)
            return Result<int>.FromResult(applicationIdResult);

        var applicationId = applicationIdResult.Value;

        var applicationIdValidation =
            LicenseValidator.ValidateApplicationId(applicationId);

        if (applicationIdValidation.IsFailure)
            return Result<int>.FromValidationFailure(
                applicationIdValidation.Error);

        var applicationResult =
            await _applicationService.GetApplicationByIdAsync(applicationId, cancellationToken);

        if (applicationResult.IsFailure)
            return Result<int>.FromResult(applicationResult);

        if (applicationResult.Value is null)
            return Result<int>.FromNotFound("Application was not found.");

        var application = applicationResult.Value;

        if (application.ApplicationTypeID != NewLocalDrivingLicenseApplicationTypeId)
            return Result<int>.FromConflict(
                "First-time license issuance is only allowed for a new local driving license application.");

        if (application.ApplicantPersonID <= 0)
            return Result<int>.FromValidationFailure(
                "The application does not have a valid applicant.");

        var personResult =
            await _personService.GetPersonByIdAsync(application.ApplicantPersonID, cancellationToken);

        if (personResult.IsFailure)
            return Result<int>.FromResult(personResult);

        if (personResult.Value is null)
            return Result<int>.FromNotFound("Applicant person was not found.");

        var person = personResult.Value;

        var classValidation =
            LicenseValidator.ValidateLicenseClassId(localApplication.LicenseClassID);

        if (classValidation.IsFailure)
            return Result<int>.FromValidationFailure(classValidation.Error);

        var classResult =
            await _licenseClassService.GetLicenseClassByIdAsync(
                localApplication.LicenseClassID,
                cancellationToken);

        if (classResult.IsFailure)
            return Result<int>.FromResult(classResult);

        if (classResult.Value is null)
            return Result<int>.FromNotFound("License class was not found.");

        var licenseClass = classResult.Value;

        if (licenseClass.DefaultValidityLength <= 0)
            return Result<int>.FromValidationFailure(
                "License class has an invalid validity period.");

        if (licenseClass.LicenseClassFees < 0)
            return Result<int>.FromValidationFailure(
                "License class has invalid fees.");

        return await ExecuteTransactionAsync(async transaction =>
        {
            var currentApplicationResult =
                await _applicationService.GetApplicationForIssuanceAsync(applicationId, cancellationToken);

            if (currentApplicationResult.IsFailure)
                return Result<int>.FromResult(currentApplicationResult);

            if (currentApplicationResult.Value is null)
                return Result<int>.FromNotFound("Application was not found.");

            if (currentApplicationResult.Value.ApplicationStatus != AppStatus.New)
                return Result<int>.FromConflict(
                    "The application is not in a valid state for license issuance.");

            if (!await _testWorkflowService.HasPassedAllTestsAsync(localAppId, cancellationToken))
                return Result<int>.FromConflict(
                    "The applicant has not passed all required tests.");

            if (await _licenseRepository.IsApplicationHasLicenseAsync(applicationId, cancellationToken))
                return Result<int>.FromConflict(
                    "A license has already been issued for this application.");

            var driverResult =
                await _driverService.GetByPersonIdAsync(person.PersonId, cancellationToken);

            int driverId;

            if (driverResult.IsSuccess)
            {
                if (driverResult.Value is null || driverResult.Value.DriverID <= 0)
                    return Result<int>.FromFailure(
                        "Driver information was returned incorrectly.");

                driverId = driverResult.Value.DriverID;
            }
            else
            {
                if (driverResult.ErrorType != ErrorType.NotFound)
                    return Result<int>.FromResult(driverResult);

                var createDriverResult = await _driverService.AddAsync(
                    new CreateDriverDto { PersonID = person.PersonId },
                    cancellationToken);

                if (createDriverResult.IsFailure)
                    return Result<int>.FromResult(createDriverResult);

                driverId = createDriverResult.Value;
            }

            var driverValidation = LicenseValidator.ValidateDriverId(driverId);
            if (driverValidation.IsFailure)
                return Result<int>.FromValidationFailure(
                    driverValidation.Error);

            if (await _licenseRepository.IsActiveLicenseExistsAsync(
                    driverId,
                    localApplication.LicenseClassID,
                    cancellationToken))
                return Result<int>.FromConflict(
                    "The driver already has an active license for this license class.");

            var issueDate = _clock.Now;

            var createLicenseDto = new CreateLicenseDto
            {
                ApplicationID = applicationId,
                DriverID = driverId,
                LicenseClassID = localApplication.LicenseClassID,
                IssueDate = issueDate,
                ExpirationDate = issueDate.AddYears(
                    licenseClass.DefaultValidityLength),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                PaidFees = licenseClass.LicenseClassFees,
                IssueReason = IssueReason.FirstTime
            };

            var licenseValidation =
                LicenseValidator.ValidateCreate(createLicenseDto);

            if (licenseValidation.IsFailure)
                return Result<int>.FromValidationFailure(
                    licenseValidation.Error);

            var license = LicenseMapper.ToEntity(createLicenseDto);
            license.CreatedByUserID = _currentUserService.UserId;

            await _licenseRepository.AddLicenseAsync(license, cancellationToken);

            if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0 ||
                license.LicenseID <= 0)
                return Result<int>.FromFailure(
                    "Failed to save the driving license.");

            var completeResult =
                await _applicationService.CompleteApplicationStateAsync(applicationId, cancellationToken);

            if (completeResult.IsFailure)
                return Result<int>.FromResult(completeResult);

            await transaction.CommitAsync(cancellationToken);

            return Result<int>.Success(license.LicenseID);
        }, cancellationToken);
    }

    private async Task<T> ExecuteTransactionAsync<T>(
        Func<IUnitOfWorkTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default) =>
        await _unitOfWork.ExecuteInTransactionAsync(
            operation,
            IsolationLevel.Serializable,
            cancellationToken);
}
