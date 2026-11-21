using Application.DTOs.TestTypeDTO;
using Domain.Entities;

namespace Application.Mappers;

public static class TestTypeMapper
{
    public static TestTypeDto ToDto(TestType entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new TestTypeDto
        {
            TestTypeId = entity.TestTypeId,
            TestTypeTitle = entity.TestTypeTitle,
            TestTypeDescription = entity.TestTypeDescription,
            TestTypeFees = entity.TestTypeFees
        };
    }
}
