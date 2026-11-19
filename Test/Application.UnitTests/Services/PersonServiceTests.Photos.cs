using Application.Common.Exceptions;
using Application.Common.Pagination;
using Application.Common.Results;
using Application.DTOs.PersonDTO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.UnitTests.Services;

public partial class PersonServiceTests
{
    // =========================================================
    // Photo Workflows
    // =========================================================

    [Fact]
    public async Task UploadPersonPhotoAsync_InvalidPersonId_ReturnsValidationFailure()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var result = await _sut.UploadPersonPhotoAsync(0, stream, 3, "image/jpeg");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UploadPersonPhotoAsync_PersonNotFound_ReturnsNotFound()
    {
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Person?)null);

        using var stream = new MemoryStream([1, 2, 3]);
        var result = await _sut.UploadPersonPhotoAsync(99, stream, 3, "image/jpeg");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UploadPersonPhotoAsync_ImageProcessingFails_ReturnsValidationFailure()
    {
        var person = CreatePerson(10);
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _imageProcessingServiceMock
            .Setup(i => i.ProcessPersonPhoto(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string?>()))
            .Returns(Result<ProcessedImageResult>.FromValidationFailure("Corrupted image"));

        using var stream = new MemoryStream([1, 2, 3]);
        var result = await _sut.UploadPersonPhotoAsync(10, stream, 3, "image/jpeg");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Corrupted image", result.Error);
    }

    [Fact]
    public async Task UploadPersonPhotoAsync_StorageFails_ReturnsFailure()
    {
        var person = CreatePerson(10);
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        var processed = new ProcessedImageResult(new MemoryStream([1, 2]), "image/webp", ".webp", 100, 100, 2);
        _imageProcessingServiceMock
            .Setup(i => i.ProcessPersonPhoto(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string?>()))
            .Returns(Result<ProcessedImageResult>.Success(processed));

        _fileStorageServiceMock
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));

        using var stream = new MemoryStream([1, 2, 3]);
        var result = await _sut.UploadPersonPhotoAsync(10, stream, 3, "image/jpeg");

        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to store image", result.Error);
    }

    [Fact]
    public async Task UploadPersonPhotoAsync_DatabaseSaveFails_CleansUpNewFileAndReturnsFailure()
    {
        var person = CreatePerson(10);
        person.ImagePath = "people/old_photo.webp";
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        var processed = new ProcessedImageResult(new MemoryStream([1, 2]), "image/webp", ".webp", 100, 100, 2);
        _imageProcessingServiceMock
            .Setup(i => i.ProcessPersonPhoto(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string?>()))
            .Returns(Result<ProcessedImageResult>.Success(processed));

        const string newKey = "people/new_photo.webp";
        _fileStorageServiceMock
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newKey);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        using var stream = new MemoryStream([1, 2, 3]);
        var result = await _sut.UploadPersonPhotoAsync(10, stream, 3, "image/jpeg");

        Assert.False(result.IsSuccess);
        _fileStorageServiceMock.Verify(s => s.DeleteAsync(newKey, It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageServiceMock.Verify(s => s.DeleteAsync("people/old_photo.webp", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadPersonPhotoAsync_Success_UpdatesDbAndCleansUpOldPhoto()
    {
        var person = CreatePerson(10);
        person.ImagePath = "people/old_photo.webp";
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        var processed = new ProcessedImageResult(new MemoryStream([1, 2]), "image/webp", ".webp", 100, 100, 2);
        _imageProcessingServiceMock
            .Setup(i => i.ProcessPersonPhoto(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string?>()))
            .Returns(Result<ProcessedImageResult>.Success(processed));

        const string newKey = "people/new_photo.webp";
        _fileStorageServiceMock
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newKey);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        using var stream = new MemoryStream([1, 2, 3]);
        var result = await _sut.UploadPersonPhotoAsync(10, stream, 3, "image/jpeg");

        Assert.True(result.IsSuccess);
        Assert.Equal(newKey, result.Value);
        Assert.Equal(newKey, person.ImagePath);

        _fileStorageServiceMock.Verify(s => s.DeleteAsync("people/old_photo.webp", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadPersonPhotoAsync_WhenConcurrencyExceptionOccurs_CleansUpNewImageAndLeavesOldImageIntact()
    {
        // Arrange
        const int personId = 10;
        const string oldImagePath = "people/old_photo.webp";
        const string newStorageKey = "people/new_photo.webp";

        var person = CreatePerson(personId);
        person.ImagePath = oldImagePath;

        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        using var testStream = new MemoryStream([1, 2, 3, 4]);
        var processed = new ProcessedImageResult(new MemoryStream([1, 2, 3]), "image/webp", ".webp", 100, 100, 3);
        _imageProcessingServiceMock
            .Setup(i => i.ProcessPersonPhoto(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string?>()))
            .Returns(Result<ProcessedImageResult>.Success(processed));

        _fileStorageServiceMock
            .Setup(f => f.SaveAsync(It.IsAny<Stream>(), ".webp", "people", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newStorageKey);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("Concurrency conflict during photo upload"));

        // Act
        var result = await _sut.UploadPersonPhotoAsync(personId, testStream, 4, "image/jpeg");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("The person record was modified by another user. Please reload and try again.", result.Error);

        _fileStorageServiceMock.Verify(
            f => f.DeleteAsync(newStorageKey, It.IsAny<CancellationToken>()),
            Times.Once);

        _fileStorageServiceMock.Verify(
            f => f.DeleteAsync(oldImagePath, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPersonPhotoAsync_PersonNotFound_ReturnsNotFound()
    {
        _personRepositoryMock
            .Setup(r => r.GetPersonByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Person?)null);

        var result = await _sut.GetPersonPhotoAsync(99);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetPersonPhotoAsync_PersonHasNoImage_ReturnsNotFound()
    {
        var person = CreatePerson(10);
        person.ImagePath = null;
        _personRepositoryMock
            .Setup(r => r.GetPersonByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        var result = await _sut.GetPersonPhotoAsync(10);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetPersonPhotoAsync_FileMissingInStorage_ReturnsNotFound()
    {
        var person = CreatePerson(10);
        person.ImagePath = "people/missing.webp";
        _personRepositoryMock
            .Setup(r => r.GetPersonByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _fileStorageServiceMock
            .Setup(s => s.GetAsync("people/missing.webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var result = await _sut.GetPersonPhotoAsync(10);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetPersonPhotoAsync_Success_ReturnsStreamAndContentType()
    {
        var person = CreatePerson(10);
        person.ImagePath = "people/photo.webp";
        _personRepositoryMock
            .Setup(r => r.GetPersonByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        var fakeStream = new MemoryStream([1, 2, 3]);
        _fileStorageServiceMock
            .Setup(s => s.GetAsync("people/photo.webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeStream);

        var result = await _sut.GetPersonPhotoAsync(10);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Stream);
        Assert.Equal("image/webp", result.Value.ContentType);
        Assert.Equal("people/photo.webp", result.Value.StorageKey);
    }

    [Fact]
    public async Task DeletePersonPhotoAsync_PersonNotFound_ReturnsNotFound()
    {
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Person?)null);

        var result = await _sut.DeletePersonPhotoAsync(99);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeletePersonPhotoAsync_PersonHasNoImage_ReturnsSuccessIdempotently()
    {
        var person = CreatePerson(10);
        person.ImagePath = null;
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        var result = await _sut.DeletePersonPhotoAsync(10);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeletePersonPhotoAsync_Success_RemovesFromDbAndDeletesFromStorage()
    {
        var person = CreatePerson(10);
        person.ImagePath = "people/old_photo.webp";
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.DeletePersonPhotoAsync(10);

        Assert.True(result.IsSuccess);
        Assert.Null(person.ImagePath);
        _fileStorageServiceMock.Verify(s => s.DeleteAsync("people/old_photo.webp", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePersonPhotoAsync_WhenConcurrencyExceptionOccurs_LeavesOldImageIntact()
    {
        // Arrange
        const int personId = 10;
        const string oldImagePath = "people/old_photo.webp";

        var person = CreatePerson(personId);
        person.ImagePath = oldImagePath;

        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("Concurrency conflict during photo delete"));

        // Act
        var result = await _sut.DeletePersonPhotoAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("The person record was modified by another user. Please reload and try again.", result.Error);

        _fileStorageServiceMock.Verify(
            f => f.DeleteAsync(oldImagePath, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePersonAsync_WhenDtoHasNullImagePath_PreservesExistingPhoto()
    {
        var person = CreatePerson(10);
        person.ImagePath = "people/precious_photo.webp";
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _personRepositoryMock
            .Setup(r => r.IsNationalNoDuplicatedAsync(It.IsAny<string>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updateDto = CreateValidUpdateDto();
        updateDto.ImagePath = null; // Ordinary update sends null

        var result = await _sut.UpdatePersonAsync(10, updateDto);

        Assert.True(result.IsSuccess);
        Assert.Equal("people/precious_photo.webp", person.ImagePath);
    }

    [Fact]
    public async Task UploadPersonPhotoAsync_WhenFileStorageThrowsOperationCanceledException_PropagatesWithoutSwallowingOrSavingToDb()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var person = CreatePerson(10);
        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(10, cts.Token))
            .ReturnsAsync(person);

        var processed = new ProcessedImageResult(new MemoryStream([1, 2]), "image/webp", ".webp", 100, 100, 2);
        _imageProcessingServiceMock
            .Setup(i => i.ProcessPersonPhoto(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string?>()))
            .Returns(Result<ProcessedImageResult>.Success(processed));

        _fileStorageServiceMock
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.UploadPersonPhotoAsync(10, stream, 3, "image/jpeg", cts.Token));

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

}
