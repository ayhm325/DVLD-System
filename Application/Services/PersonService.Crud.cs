using System.Collections;

using Application.Common.Exceptions;
using Application.Common.Results;
using Application.DTOs.PersonDTO;
using Application.Mappers;
using Application.Validators;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed partial class PersonService
{
    public async Task<Result<int>>
        AddPersonAsync(
            PersonCreateDto personDto,
            CancellationToken cancellationToken = default)
    {
        var validation =
            PersonValidator.Validate(
                personDto,
                _clock.Today);

        if (validation.IsFailure)
        {
            return Result<int>
                .FromValidationFailure(
                    validation.Error);
        }

        var person =
            PersonMapper.ToEntity(
                personDto!);

        var duplicated =
            await _personRepository
                .IsNationalNoDuplicatedAsync(
                    person.NationalNo,
                    0,
                    cancellationToken);

        if (duplicated)
        {
            return Result<int>
                .FromConflict(
                    "The national number is already registered.");
        }

        await _personRepository
            .AddPersonAsync(person, cancellationToken);

        var saved =
            await _unitOfWork
                .SaveChangesAsync(cancellationToken);

        if (saved <= 0 ||
            person.PersonId <= 0)
        {
            return Result<int>
                .FromFailure(
                    "Failed to create person.");
        }

        return Result<int>
            .Success(
                person.PersonId);
    }

    public async Task<Result>
        UpdatePersonAsync(
            int id,
            PersonUpdateDto personDto,
            CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.ValidationFailure(
                "Invalid person ID.");
        }

        var validation =
            PersonValidator.Validate(
                personDto,
                _clock.Today);

        if (validation.IsFailure)
            return validation;

        byte[] expectedRowVersion;
        try
        {
            expectedRowVersion = Convert.FromBase64String(personDto!.RowVersion);
        }
        catch (FormatException)
        {
            return Result.ValidationFailure("Invalid row version token.");
        }

        var existingPerson =
            await _personRepository
                .GetPersonForUpdateAsync(id, cancellationToken);

        if (existingPerson is null)
        {
            return Result.NotFound(
                "Person not found.");
        }

        var normalizedNationalNo =
            personDto.NationalNo.Trim();

        var duplicated =
            await _personRepository
                .IsNationalNoDuplicatedAsync(
                    normalizedNationalNo,
                    id,
                    cancellationToken);

        if (duplicated)
        {
            return Result.Conflict(
                "The national number is already registered to another person.");
        }

        bool hasChanges =
            !string.Equals(existingPerson.NationalNo, normalizedNationalNo, StringComparison.Ordinal) ||
            !string.Equals(existingPerson.FirstName, personDto.FirstName.Trim(), StringComparison.Ordinal) ||
            !string.Equals(existingPerson.SecondName, personDto.SecondName.Trim(), StringComparison.Ordinal) ||
            !string.Equals(existingPerson.ThirdName ?? string.Empty, (personDto.ThirdName ?? string.Empty).Trim(), StringComparison.Ordinal) ||
            !string.Equals(existingPerson.LastName, personDto.LastName.Trim(), StringComparison.Ordinal) ||
            existingPerson.DateOfBirth.Date != personDto.DateOfBirth.Date ||
            (int)existingPerson.Gender != personDto.Gender ||
            !string.Equals(existingPerson.Address, personDto.Address.Trim(), StringComparison.Ordinal) ||
            !string.Equals(existingPerson.Phone, personDto.Phone.Trim(), StringComparison.Ordinal) ||
            !string.Equals(existingPerson.Email ?? string.Empty, (personDto.Email ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) ||
            existingPerson.NationalityCountryID != personDto.NationalityCountryID;

        if (!hasChanges)
        {
            if (existingPerson.RowVersion != null &&
                existingPerson.RowVersion.Length > 0 &&
                !StructuralComparisons.StructuralEqualityComparer.Equals(expectedRowVersion, existingPerson.RowVersion))
            {
                return Result.Conflict(
                    "The person record was modified by another user. Please reload and try again.");
            }

            return Result.Success();
        }

        _personRepository.SetOriginalRowVersion(existingPerson, expectedRowVersion);

        PersonMapper.ApplyUpdate(
            personDto,
            existingPerson);

        try
        {
            var saved =
                await _unitOfWork
                    .SaveChangesAsync(cancellationToken);

            if (saved <= 0)
            {
                return Result.Failure(
                    "No changes were saved.");
            }

            return Result.Success();
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating person {PersonId}", id);
            return Result.Conflict(
                "The person record was modified by another user. Please reload and try again.");
        }
    }

    public async Task<Result>
        DeletePersonAsync(
            int id,
            CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.ValidationFailure(
                "Invalid person ID.");
        }

        var person =
            await _personRepository
                .GetPersonForUpdateAsync(id, cancellationToken);

        if (person is null)
        {
            return Result.NotFound(
                "Person not found.");
        }

        if (await _personRepository
                .HasApplicationsAsync(id, cancellationToken))
        {
            return Result.Conflict(
                "Cannot delete this person because they have one or more applications.");
        }

        var imagePathToDelete = person.ImagePath;

        var removed =
            await _personRepository
                .DeletePersonAsync(id, cancellationToken);

        if (!removed)
        {
            return Result.Failure(
                "Failed to delete person.");
        }

        try
        {
            var saved =
                await _unitOfWork
                    .SaveChangesAsync(cancellationToken);

            if (saved <= 0)
            {
                return Result.Failure(
                    "Failed to save person deletion.");
            }
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during deletion of person {PersonId}", id);
            return Result.Conflict(
                "The person record was modified by another user. Please reload and try again.");
        }

        if (!string.IsNullOrWhiteSpace(imagePathToDelete))
        {
            try
            {
                await _fileStorageService.DeleteAsync(imagePathToDelete, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up image {ImagePath} for deleted person {PersonId}", imagePathToDelete, id);
            }
        }

        return Result.Success();
    }
}
