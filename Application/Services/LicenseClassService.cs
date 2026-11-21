using Application.Common.Results;
using Application.DTOs;
using Application.Interfaces;
using Application.Mappers;
using Application.Validators;

namespace Application.Services;

public sealed class LicenseClassService(
    ILicenseClassRepository licenseClassRepository,
    IUnitOfWork unitOfWork) : ILicenseClassService
{
    private readonly ILicenseClassRepository _licenseClassRepository =
        licenseClassRepository
        ?? throw new ArgumentNullException(nameof(licenseClassRepository));

    private readonly IUnitOfWork _unitOfWork =
        unitOfWork
        ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async Task<Result<List<LicenseClassDto>>>
        GetAllLicenseClassesAsync(CancellationToken cancellationToken = default)
    {
        var licenseClasses =
            await _licenseClassRepository
                .GetAllLicenseClassAsync(cancellationToken);

        return Result<List<LicenseClassDto>>.Success(
            licenseClasses
                .Select(LicenseClassMapper.ToDto)
                .ToList());
    }

    public async Task<Result<LicenseClassDto>>
        GetLicenseClassByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var validation =
            LicenseClassValidator.ValidateId(id);

        if (validation.IsFailure)
        {
            return Result<LicenseClassDto>.FromValidationFailure(
                validation.Error);
        }

        var licenseClass =
            await _licenseClassRepository
                .GetLicenseClassByIdAsync(id, cancellationToken);

        return licenseClass is null
            ? Result<LicenseClassDto>.FromNotFound(
                "License class not found.")
            : Result<LicenseClassDto>.Success(
                LicenseClassMapper.ToDto(licenseClass));
    }

    public async Task<Result> UpdateLicenseClassAsync(
        int id,
        LicenseClassDto dto,
        CancellationToken cancellationToken = default)
    {
        var validation =
            LicenseClassValidator.ValidateUpdate(id, dto);

        if (validation.IsFailure)
        {
            return Result.ValidationFailure(
                validation.Error);
        }

        var licenseClass =
            await _licenseClassRepository
                .GetForUpdateAsync(id, cancellationToken);

        if (licenseClass is null)
        {
            return Result.NotFound(
                "License class not found.");
        }

        bool hasChanges =
            licenseClass.ClassName != dto.LicenseClassName.Trim() ||
            licenseClass.ClassDescription != dto.LicenseClassDescription.Trim() ||
            licenseClass.MinimumAllowedAge != dto.MinAllowedAge ||
            licenseClass.DefaultValidityLength != dto.DefaultValidityLength ||
            licenseClass.ClassFees != dto.LicenseClassFees;

        if (!hasChanges)
        {
            return Result.Success();
        }

        licenseClass.ClassName = dto.LicenseClassName.Trim();
        licenseClass.ClassDescription = dto.LicenseClassDescription.Trim();
        licenseClass.MinimumAllowedAge = dto.MinAllowedAge;
        licenseClass.DefaultValidityLength = dto.DefaultValidityLength;
        licenseClass.ClassFees = dto.LicenseClassFees;

        var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

        return saved > 0
            ? Result.Success()
            : Result.Failure("Failed to update license class.");
    }
}
