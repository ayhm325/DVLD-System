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
    // AddPersonAsync
    // =========================================================

    [Fact]
    public async Task AddPersonAsync_InvalidDto_ReturnsValidationFailure()
    {
        // Arrange
        var dto = new PersonCreateDto
        {
            NationalNo = "123",
            FirstName = "",
            SecondName = "",
            LastName = "",
            DateOfBirth = _clock.Now.AddYears(1),
            Gender = (int)Gender.Male,
            Address = "",
            Phone = "",
            Email = null,
            NationalityCountryID = 0
        };

        // Act
        Result<int> result =
            await _sut.AddPersonAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);

        _personRepositoryMock.Verify(
            repository =>
                repository.IsNationalNoDuplicatedAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _personRepositoryMock.Verify(
            repository => repository.AddPersonAsync(
                It.IsAny<Person>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddPersonAsync_DuplicateNationalNumber_ReturnsConflict()
    {
        // Arrange
        var dto = CreateValidCreateDto();

        _personRepositoryMock
            .Setup(repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    0,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result<int> result =
            await _sut.AddPersonAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "The national number is already registered.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    0,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _personRepositoryMock.Verify(
            repository =>
                repository.AddPersonAsync(
                    It.IsAny<Person>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddPersonAsync_SaveFails_ReturnsFailure()
    {
        // Arrange
        var dto = CreateValidCreateDto();

        _personRepositoryMock
            .Setup(repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    0,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _personRepositoryMock
            .Setup(repository =>
                repository.AddPersonAsync(
                    It.IsAny<Person>(),
                    It.IsAny<CancellationToken>()))
            .Callback<Person, CancellationToken>((person, _) =>
                person.PersonId = 1)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        Result<int> result =
            await _sut.AddPersonAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to create person.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.AddPersonAsync(
                    It.IsAny<Person>(),
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddPersonAsync_ValidDto_ReturnsCreatedPersonId()
    {
        // Arrange
        var dto = CreateValidCreateDto();

        _personRepositoryMock
            .Setup(repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    0,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _personRepositoryMock
            .Setup(repository =>
                repository.AddPersonAsync(
                    It.IsAny<Person>(),
                    It.IsAny<CancellationToken>()))
            .Callback<Person, CancellationToken>((person, _) =>
                person.PersonId = 25)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result<int> result =
            await _sut.AddPersonAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Equal(25, result.Value);

        _personRepositoryMock.Verify(
            repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    0,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _personRepositoryMock.Verify(
            repository =>
                repository.AddPersonAsync(
                    It.Is<Person>(person =>
                        person.NationalNo == dto.NationalNo &&
                        person.FirstName == dto.FirstName &&
                        person.LastName == dto.LastName),
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // UpdatePersonAsync
    // =========================================================

    [Fact]
    public async Task UpdatePersonAsync_InvalidId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidPersonId = 0;
        var dto = CreateValidUpdateDto();

        // Act
        Result result =
            await _sut.UpdatePersonAsync(
                invalidPersonId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Invalid person ID.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonForUpdateAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePersonAsync_InvalidDto_ReturnsValidationFailure()
    {
        // Arrange
        const int personId = 10;

        var dto = new PersonUpdateDto
        {
            NationalNo = "123",
            FirstName = "",
            SecondName = "",
            LastName = "",
            DateOfBirth = _clock.Now.AddYears(1),
            Gender = (int)Gender.Male,
            Address = "",
            Phone = "",
            Email = null,
            NationalityCountryID = 0
        };

        // Act
        Result result =
            await _sut.UpdatePersonAsync(
                personId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonForUpdateAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePersonAsync_PersonNotFound_ReturnsNotFound()
    {
        // Arrange
        const int personId = 10;
        var dto = CreateValidUpdateDto();

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Person?)null);

        // Act
        Result result =
            await _sut.UpdatePersonAsync(
                personId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Person not found.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePersonAsync_DuplicateNationalNumber_ReturnsConflict()
    {
        // Arrange
        const int personId = 10;
        var dto = CreateValidUpdateDto();

        var existingPerson = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPerson);

        _personRepositoryMock
            .Setup(repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    personId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result result =
            await _sut.UpdatePersonAsync(
                personId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "The national number is already registered to another person.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    personId,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePersonAsync_SaveFails_ReturnsFailure()
    {
        // Arrange
        const int personId = 10;
        var dto = CreateValidUpdateDto();

        var existingPerson = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPerson);

        _personRepositoryMock
            .Setup(repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    personId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        Result result =
            await _sut.UpdatePersonAsync(
                personId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "No changes were saved.",
            result.Error);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePersonAsync_ValidDto_ReturnsSuccess()
    {
        // Arrange
        const int personId = 10;
        var dto = CreateValidUpdateDto();

        var existingPerson = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPerson);

        _personRepositoryMock
            .Setup(repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    personId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result result =
            await _sut.UpdatePersonAsync(
                personId,
                dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);

        Assert.Equal(dto.NationalNo, existingPerson.NationalNo);
        Assert.Equal(dto.FirstName, existingPerson.FirstName);
        Assert.Equal(dto.SecondName, existingPerson.SecondName);
        Assert.Equal(dto.LastName, existingPerson.LastName);
        Assert.Equal(dto.Phone, existingPerson.Phone);
        Assert.Equal(dto.Email, existingPerson.Email);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);

        _personRepositoryMock.Verify(
            repository =>
                repository.IsNationalNoDuplicatedAsync(
                    dto.NationalNo,
                    personId,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePersonAsync_WhenConcurrencyConflictExceptionOccurs_ReturnsConflict()
    {
        // Arrange
        const int personId = 10;
        var dto = CreateValidUpdateDto();
        var existingPerson = CreatePerson(personId);

        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPerson);

        _personRepositoryMock
            .Setup(r => r.IsNationalNoDuplicatedAsync(dto.NationalNo, personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("Concurrency conflict occurred"));

        // Act
        Result result = await _sut.UpdatePersonAsync(personId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("The person record was modified by another user. Please reload and try again.", result.Error);
    }

    [Fact]
    public async Task UpdatePersonAsync_NoOpWithCurrentRowVersion_ReturnsSuccessWithoutSaving()
    {
        // Arrange
        const int personId = 10;
        var existingPerson = CreatePerson(personId);
        var dto = new PersonUpdateDto
        {
            NationalNo = existingPerson.NationalNo,
            FirstName = existingPerson.FirstName,
            SecondName = existingPerson.SecondName,
            ThirdName = existingPerson.ThirdName,
            LastName = existingPerson.LastName,
            DateOfBirth = existingPerson.DateOfBirth,
            Gender = (int)existingPerson.Gender,
            Address = existingPerson.Address,
            Phone = existingPerson.Phone,
            Email = existingPerson.Email,
            NationalityCountryID = existingPerson.NationalityCountryID,
            ImagePath = existingPerson.ImagePath,
            RowVersion = DefaultRowVersionBase64
        };

        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPerson);

        _personRepositoryMock
            .Setup(r => r.IsNationalNoDuplicatedAsync(dto.NationalNo, personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.UpdatePersonAsync(personId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _personRepositoryMock.Verify(r => r.SetOriginalRowVersion(It.IsAny<Person>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePersonAsync_NoOpWithStaleRowVersion_ReturnsConflictWithoutSaving()
    {
        // Arrange
        const int personId = 10;
        var existingPerson = CreatePerson(personId);
        byte[] staleToken = [9, 9, 9, 9, 9, 9, 9, 9];
        var dto = new PersonUpdateDto
        {
            NationalNo = existingPerson.NationalNo,
            FirstName = existingPerson.FirstName,
            SecondName = existingPerson.SecondName,
            ThirdName = existingPerson.ThirdName,
            LastName = existingPerson.LastName,
            DateOfBirth = existingPerson.DateOfBirth,
            Gender = (int)existingPerson.Gender,
            Address = existingPerson.Address,
            Phone = existingPerson.Phone,
            Email = existingPerson.Email,
            NationalityCountryID = existingPerson.NationalityCountryID,
            ImagePath = existingPerson.ImagePath,
            RowVersion = Convert.ToBase64String(staleToken)
        };

        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPerson);

        _personRepositoryMock
            .Setup(r => r.IsNationalNoDuplicatedAsync(dto.NationalNo, personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.UpdatePersonAsync(personId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("The person record was modified by another user. Please reload and try again.", result.Error);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePersonAsync_WithChanges_PassesExpectedRowVersionToRepository()
    {
        // Arrange
        const int personId = 10;
        var dto = CreateValidUpdateDto();
        var existingPerson = CreatePerson(personId);

        _personRepositoryMock
            .Setup(r => r.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPerson);

        _personRepositoryMock
            .Setup(r => r.IsNationalNoDuplicatedAsync(dto.NationalNo, personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.UpdatePersonAsync(personId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        _personRepositoryMock.Verify(
            r => r.SetOriginalRowVersion(existingPerson, It.Is<byte[]>(b => b.SequenceEqual(DefaultRowVersion))),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePersonAsync_InvalidRowVersionBase64_ReturnsValidationFailure()
    {
        // Arrange
        const int personId = 10;
        var dto = CreateValidUpdateDto();
        dto.RowVersion = "not-valid-base64!";

        // Act
        var result = await _sut.UpdatePersonAsync(personId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        _personRepositoryMock.Verify(r => r.GetPersonForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================
    // DeletePersonAsync
    // =========================================================

    [Fact]
    public async Task DeletePersonAsync_InvalidId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidPersonId = 0;

        // Act
        Result result =
            await _sut.DeletePersonAsync(invalidPersonId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Invalid person ID.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonForUpdateAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeletePersonAsync_PersonNotFound_ReturnsNotFound()
    {
        // Arrange
        const int personId = 10;

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Person?)null);

        // Act
        Result result =
            await _sut.DeletePersonAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Person not found.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletePersonAsync_HasApplications_ReturnsConflict()
    {
        // Arrange
        const int personId = 10;

        var person = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _personRepositoryMock
            .Setup(repository =>
                repository.HasApplicationsAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result result =
            await _sut.DeletePersonAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "Cannot delete this person because they have one or more applications.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.DeletePersonAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeletePersonAsync_DeleteFails_ReturnsFailure()
    {
        // Arrange
        const int personId = 10;

        var person = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _personRepositoryMock
            .Setup(repository =>
                repository.HasApplicationsAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _personRepositoryMock
            .Setup(repository =>
                repository.DeletePersonAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        Result result =
            await _sut.DeletePersonAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to delete person.",
            result.Error);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeletePersonAsync_SaveFails_ReturnsFailure()
    {
        // Arrange
        const int personId = 10;

        var person = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _personRepositoryMock
            .Setup(repository =>
                repository.HasApplicationsAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _personRepositoryMock
            .Setup(repository =>
                repository.DeletePersonAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        Result result =
            await _sut.DeletePersonAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to save person deletion.",
            result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.DeletePersonAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletePersonAsync_ValidPerson_ReturnsSuccess()
    {
        // Arrange
        const int personId = 10;

        var person = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _personRepositoryMock
            .Setup(repository =>
                repository.HasApplicationsAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _personRepositoryMock
            .Setup(repository =>
                repository.DeletePersonAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result result =
            await _sut.DeletePersonAsync(personId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);

        _personRepositoryMock.Verify(
            repository =>
                repository.HasApplicationsAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);

        _personRepositoryMock.Verify(
            repository =>
                repository.DeletePersonAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletePersonAsync_WhenConcurrencyConflictExceptionOccurs_ReturnsConflict()
    {
        // Arrange
        const int personId = 10;
        var person = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository => repository.GetPersonForUpdateAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        _personRepositoryMock
            .Setup(repository => repository.HasApplicationsAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _personRepositoryMock
            .Setup(repository => repository.DeletePersonAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _unitOfWorkMock
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("Concurrency conflict during person delete"));

        // Act
        var result = await _sut.DeletePersonAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("The person record was modified by another user. Please reload and try again.", result.Error);
    }

    // =========================================================
    [Theory]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(-256)]
    public async Task AddPersonAsync_GenderExceedsByteRange_ReturnsValidationFailure(
        int invalidGender)
    {
        var dto = new PersonCreateDto
        {
            NationalNo = "1234567890",
            FirstName = "Ayhm",
            SecondName = "Mohammed",
            ThirdName = null,
            LastName = "Obeidat",
            DateOfBirth = new DateTime(1993, 1, 1),
            Gender = invalidGender,
            Address = "Amman",
            Phone = "0791234567",
            Email = "ayhm@example.com",
            NationalityCountryID = 1,
            ImagePath = null
        };

        Result<int> result = await _sut.AddPersonAsync(dto);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Invalid gender value.", result.Error);

        _personRepositoryMock.Verify(
            repository => repository.AddPersonAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
