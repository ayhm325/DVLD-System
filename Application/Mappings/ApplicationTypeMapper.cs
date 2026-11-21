using Application.DTOs;
using Domain.Entities;

namespace Application.Mappers;

public static class ApplicationTypeMapper
{
    public static ApplicationTypeDto ToDto(ApplicationType entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new ApplicationTypeDto
        {
            ApplicationTypeId = entity.ApplicationTypeId,
            ApplicationTypeTitle = entity.ApplicationTypeTitle,
            ApplicationTypeFees = entity.ApplicationFees
        };
    }
}
