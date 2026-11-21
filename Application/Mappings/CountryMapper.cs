using Application.DTOs.CountryDTO;
using Domain.Entities;

namespace Application.Mappers;

public static class CountryMapper
{
    public static CountryDto ToDto(Country country)
    {
        return new CountryDto
        {
            CountryId = country.CountryId,
            CountryName = country.CountryName
        };
    }
}