using Application.DTOs;
using Domain.Entities;

namespace Application.Mappers;

public static class LicenseClassMapper
{
    public static LicenseClassDto ToDto(LicenseClass entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new LicenseClassDto
        {
            LicenseClassID = entity.LicenseClassID,
            LicenseClassName = entity.ClassName,
            LicenseClassDescription = entity.ClassDescription,
            MinAllowedAge = entity.MinimumAllowedAge,
            DefaultValidityLength = entity.DefaultValidityLength,
            LicenseClassFees = entity.ClassFees
        };
    }
}
