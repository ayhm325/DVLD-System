using Application.Common.Exceptions;
using Application.Common.Results;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed partial class PersonService
{
    public async Task<Result<string>> UploadPersonPhotoAsync(
        int personId,
        Stream photoStream,
        long length,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (personId <= 0)
        {
            return Result<string>.FromValidationFailure("Invalid person ID.");
        }

        var person = await _personRepository.GetPersonForUpdateAsync(personId, cancellationToken);
        if (person is null)
        {
            return Result<string>.FromNotFound("Person not found.");
        }

        var processingResult = _imageProcessingService.ProcessPersonPhoto(
            photoStream,
            length,
            contentType);

        if (processingResult.IsFailure)
        {
            return Result<string>.FromValidationFailure(processingResult.Error);
        }

        var processed = processingResult.Value!;
        string newStorageKey;
        try
        {
            newStorageKey = await _fileStorageService.SaveAsync(
                processed.ImageStream,
                processed.Extension,
                "people",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist processed image file for person {PersonId}", personId);
            return Result<string>.FromFailure("Failed to store image file.");
        }

        var oldImagePath = person.ImagePath;
        person.ImagePath = newStorageKey;

        try
        {
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved <= 0)
            {
                _logger.LogWarning("Database save failed for person {PersonId} photo update. Performing cleanup of {StorageKey}", personId, newStorageKey);
                await _fileStorageService.DeleteAsync(newStorageKey, cancellationToken);
                return Result<string>.FromFailure("Failed to save person photo reference to the database.");
            }
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during photo upload for person {PersonId}. Cleaning up {StorageKey}", personId, newStorageKey);
            try
            {
                await _fileStorageService.DeleteAsync(newStorageKey, cancellationToken);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up image {StorageKey} after concurrency conflict", newStorageKey);
            }
            return Result<string>.FromConflict("The person record was modified by another user. Please reload and try again.");
        }

        if (!string.IsNullOrWhiteSpace(oldImagePath) &&
            !string.Equals(oldImagePath, newStorageKey, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _fileStorageService.DeleteAsync(oldImagePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up superseded photo {OldKey} for person {PersonId}", oldImagePath, personId);
            }
        }

        return Result<string>.Success(newStorageKey);
    }

    public async Task<Result<(Stream Stream, string ContentType, string StorageKey)>> GetPersonPhotoAsync(
        int personId,
        CancellationToken cancellationToken = default)
    {
        if (personId <= 0)
        {
            return Result<(Stream, string, string)>.FromValidationFailure("Invalid person ID.");
        }

        var person = await _personRepository.GetPersonByIdAsync(personId, cancellationToken);
        if (person is null)
        {
            return Result<(Stream, string, string)>.FromNotFound("Person not found.");
        }

        if (string.IsNullOrWhiteSpace(person.ImagePath))
        {
            return Result<(Stream, string, string)>.FromNotFound("Person does not have a photograph.");
        }

        var stream = await _fileStorageService.GetAsync(person.ImagePath, cancellationToken);
        if (stream is null)
        {
            return Result<(Stream, string, string)>.FromNotFound("Photograph file could not be found.");
        }

        string contentType = person.ImagePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            ? "image/webp"
            : person.ImagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";

        return Result<(Stream, string, string)>.Success((stream, contentType, person.ImagePath));
    }

    public async Task<Result> DeletePersonPhotoAsync(
        int personId,
        CancellationToken cancellationToken = default)
    {
        if (personId <= 0)
        {
            return Result.ValidationFailure("Invalid person ID.");
        }

        var person = await _personRepository.GetPersonForUpdateAsync(personId, cancellationToken);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        if (string.IsNullOrWhiteSpace(person.ImagePath))
        {
            return Result.Success();
        }

        var oldImagePath = person.ImagePath;
        person.ImagePath = null;

        try
        {
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved <= 0)
            {
                return Result.Failure("Failed to remove photo reference from the database.");
            }
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during photo delete for person {PersonId}", personId);
            return Result.Conflict("The person record was modified by another user. Please reload and try again.");
        }

        try
        {
            await _fileStorageService.DeleteAsync(oldImagePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up removed image {OldKey} for person {PersonId}", oldImagePath, personId);
        }

        return Result.Success();
    }
}
