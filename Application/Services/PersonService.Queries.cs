using Application.Common.Pagination;
using Application.Common.Results;
using Application.DTOs.PersonDTO;
using Application.Mappers;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed partial class PersonService
{
    public Task<Result<PagedResult<PersonDto>>>
    GetAllPeopleAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request is PersonQueryRequest queryRequest)
        {
            return GetAllPeopleAsync(queryRequest, cancellationToken);
        }

        return GetAllInternalAsync(() => _personRepository.GetAllAsync(request, cancellationToken));
    }

    public Task<Result<PagedResult<PersonDto>>>
    GetAllPeopleAsync(
        PersonQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return GetAllInternalAsync(() => _personRepository.GetAllAsync(request, cancellationToken));
    }

    public async Task<Result<PagedResult<PersonDto>>>
    GetPeopleAvailableForUserAsync(
        PersonQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await GetAllInternalAsync(() => _personRepository.GetPeopleAvailableForUserAsync(request, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve available people.");
            return Result<PagedResult<PersonDto>>.FromFailure("Failed to retrieve available people.");
        }
    }

    private async Task<Result<PagedResult<PersonDto>>>
    GetAllInternalAsync(Func<Task<PagedResult<Person>>> fetcher)
    {
        var pagedEntities = await fetcher();
        return Result<PagedResult<PersonDto>>.Success(
            pagedEntities.Map(PersonMapper.ToDto));
    }

    public async Task<Result<List<PersonDto>>>
        GetAllPeopleAsync(
            CancellationToken cancellationToken = default)
    {
        var people =
            await _personRepository
                .GetAllPersonsAsync(cancellationToken);

        var peopleDto =
            people
                .Select(PersonMapper.ToDto)
                .ToList();

        return Result<List<PersonDto>>
            .Success(peopleDto);
    }

    public async Task<Result<PersonStatisticsDto>> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _personRepository.GetStatisticsAsync(cancellationToken);
            return Result<PersonStatisticsDto>.Success(statistics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve people statistics.");
            return Result<PersonStatisticsDto>.FromFailure("Failed to retrieve people statistics.");
        }
    }

    public async Task<Result<PersonDto>>
        GetPersonByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<PersonDto>
                .FromValidationFailure(
                    "Invalid person ID.");
        }

        var person =
            await _personRepository
                .GetPersonByIdAsync(id, cancellationToken);

        if (person is null)
        {
            return Result<PersonDto>
                .FromNotFound(
                    "Person not found.");
        }

        return Result<PersonDto>
            .Success(
                PersonMapper.ToDto(person));
    }

    public async Task<Result<PersonDto>>
        GetPersonByNationalNoAsync(
            string nationalNo,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nationalNo))
        {
            return Result<PersonDto>
                .FromValidationFailure(
                    "National number is required.");
        }

        var normalizedNationalNo =
            nationalNo.Trim();

        if (normalizedNationalNo.Length != 10 ||
            normalizedNationalNo.Any(
                c => !char.IsDigit(c)))
        {
            return Result<PersonDto>
                .FromValidationFailure(
                    "National number must be exactly 10 digits.");
        }

        var person =
            await _personRepository
                .GetPersonByNationalNoAsync(
                    normalizedNationalNo,
                    cancellationToken);

        if (person is null)
        {
            return Result<PersonDto>
                .FromNotFound(
                    "Person not found.");
        }

        return Result<PersonDto>
            .Success(
                PersonMapper.ToDto(person));
    }

    public Task<bool>
        IsPersonExistsAsync(
            int id,
            CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Task.FromResult(false);

        return _personRepository
            .IsPersonExistsByIdAsync(id, cancellationToken);
    }
}
