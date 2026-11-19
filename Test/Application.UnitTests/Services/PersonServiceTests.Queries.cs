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
    // GetPersonByIdAsync
    // =========================================================

    [Fact]
    public async Task GetPersonByIdAsync_InvalidId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidPersonId = 0;

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByIdAsync(invalidPersonId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Invalid person ID.", result.Error);

        _personRepositoryMock.Verify(
            repository => repository.GetPersonByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPersonByIdAsync_PersonNotFound_ReturnsNotFound()
    {
        // Arrange
        const int personId = 10;

        _personRepositoryMock
            .Setup(repository => repository.GetPersonByIdAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Person?)null);

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByIdAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal("Person not found.", result.Error);

        _personRepositoryMock.Verify(
            repository => repository.GetPersonByIdAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPersonByIdAsync_PersonExists_ReturnsSuccess()
    {
        // Arrange
        const int personId = 10;

        var person = CreatePerson(personId);

        _personRepositoryMock
            .Setup(repository => repository.GetPersonByIdAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByIdAsync(personId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.NotNull(result.Value);

        Assert.Equal(person.PersonId, result.Value!.PersonId);
        Assert.Equal(person.NationalNo, result.Value.NationalNo);
        Assert.Equal(person.FirstName, result.Value.FirstName);
        Assert.Equal(person.SecondName, result.Value.SecondName);
        Assert.Equal(person.LastName, result.Value.LastName);
        Assert.Equal(person.DateOfBirth, result.Value.DateOfBirth);
        Assert.Equal((int)person.Gender, result.Value.Gender);
        Assert.Equal(person.Address, result.Value.Address);
        Assert.Equal(person.Phone, result.Value.Phone);
        Assert.Equal(person.Email, result.Value.Email);
        Assert.Equal(
            person.NationalityCountryID,
            result.Value.NationalityCountryID);

        _personRepositoryMock.Verify(
            repository => repository.GetPersonByIdAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // GetPersonByNationalNoAsync
    // =========================================================

    [Fact]
    public async Task GetPersonByNationalNoAsync_EmptyNationalNo_ReturnsValidationFailure()
    {
        // Arrange
        const string nationalNo = "   ";

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByNationalNoAsync(nationalNo);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "National number is required.",
            result.Error);

        _personRepositoryMock.Verify(
            repository => repository.GetPersonByNationalNoAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPersonByNationalNoAsync_InvalidFormat_ReturnsValidationFailure()
    {
        // Arrange
        const string nationalNo = "12345";

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByNationalNoAsync(nationalNo);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "National number must be exactly 10 digits.",
            result.Error);

        _personRepositoryMock.Verify(
            repository => repository.GetPersonByNationalNoAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPersonByNationalNoAsync_PersonNotFound_ReturnsNotFound()
    {
        // Arrange
        const string nationalNo = "1234567890";

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonByNationalNoAsync(nationalNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Person?)null);

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByNationalNoAsync(nationalNo);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal("Person not found.", result.Error);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonByNationalNoAsync(nationalNo, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPersonByNationalNoAsync_ValidNationalNo_ReturnsSuccess()
    {
        // Arrange
        const string nationalNo = "1234567890";

        var person = CreatePerson(
            personId: 10,
            nationalNo: nationalNo);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonByNationalNoAsync(nationalNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByNationalNoAsync(nationalNo);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.NotNull(result.Value);

        Assert.Equal(person.PersonId, result.Value!.PersonId);
        Assert.Equal(nationalNo, result.Value.NationalNo);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonByNationalNoAsync(nationalNo, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPersonByNationalNoAsync_NationalNoHasWhitespace_TrimsBeforeRepositoryCall()
    {
        // Arrange
        const string inputNationalNo = " 1234567890 ";
        const string normalizedNationalNo = "1234567890";

        var person = CreatePerson(
            personId: 10,
            nationalNo: normalizedNationalNo);

        _personRepositoryMock
            .Setup(repository =>
                repository.GetPersonByNationalNoAsync(normalizedNationalNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(person);

        // Act
        Result<PersonDto> result =
            await _sut.GetPersonByNationalNoAsync(inputNationalNo);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(
            normalizedNationalNo,
            result.Value!.NationalNo);

        _personRepositoryMock.Verify(
            repository =>
                repository.GetPersonByNationalNoAsync(
                    normalizedNationalNo,
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // IsPersonExistsAsync
    // =========================================================

    [Fact]
    public async Task IsPersonExistsAsync_InvalidId_ReturnsFalseWithoutRepositoryCall()
    {
        // Arrange
        const int invalidPersonId = 0;

        // Act
        bool result =
            await _sut.IsPersonExistsAsync(invalidPersonId);

        // Assert
        Assert.False(result);

        _personRepositoryMock.Verify(
            repository =>
                repository.IsPersonExistsByIdAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IsPersonExistsAsync_ValidId_DelegatesToRepository()
    {
        // Arrange
        const int personId = 10;

        _personRepositoryMock
            .Setup(repository =>
                repository.IsPersonExistsByIdAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        bool result =
            await _sut.IsPersonExistsAsync(personId);

        // Assert
        Assert.True(result);

        _personRepositoryMock.Verify(
            repository =>
                repository.IsPersonExistsByIdAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // GetAllPeopleAsync
    // =========================================================

    [Fact]
    public async Task GetAllPeopleAsync_PeopleExist_ReturnsMappedPeople()
    {
        // Arrange
        var people = new List<Person>
        {
            CreatePerson(1, "1234567890"),
            CreatePerson(2, "0987654321")
        };

        _personRepositoryMock
            .Setup(repository => repository.GetAllPersonsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(people);

        // Act
        Result<List<PersonDto>> result =
            await _sut.GetAllPeopleAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.NotNull(result.Value);

        Assert.Equal(2, result.Value!.Count);

        Assert.Equal(
            people[0].PersonId,
            result.Value[0].PersonId);

        Assert.Equal(
            people[0].NationalNo,
            result.Value[0].NationalNo);

        Assert.Equal(
            people[1].PersonId,
            result.Value[1].PersonId);

        Assert.Equal(
            people[1].NationalNo,
            result.Value[1].NationalNo);

        _personRepositoryMock.Verify(
            repository => repository.GetAllPersonsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllPeopleAsync_WithQueryRequest_ReturnsMappedPagedResult()
    {
        // Arrange
        var request = new PersonQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = "Ahmad",
            SearchBy = "name",
            GenderFilter = "0"
        };

        var pagedPeople = new PagedResult<Person>
        {
            Items = new List<Person>
            {
                CreatePerson(1, "1234567890"),
                CreatePerson(2, "0987654321")
            },
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 2
        };

        _personRepositoryMock
            .Setup(r => r.GetAllAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedPeople);

        // Act
        var result = await _sut.GetAllPeopleAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(1, result.Value.PageNumber);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(1, result.Value.Items[0].PersonId);
        Assert.Equal(2, result.Value.Items[1].PersonId);

        _personRepositoryMock.Verify(r => r.GetAllAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllPeopleAsync_WithPaginationRequest_DelegatesToRepository()
    {
        // Arrange
        var request = new PaginationRequest
        {
            PageNumber = 2,
            PageSize = 5
        };

        var pagedPeople = new PagedResult<Person>
        {
            Items = new List<Person>(),
            PageNumber = 2,
            PageSize = 5,
            TotalCount = 10
        };

        _personRepositoryMock
            .Setup(r => r.GetAllAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedPeople);

        // Act
        var result = await _sut.GetAllPeopleAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(10, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(5, result.Value.PageSize);
        Assert.Empty(result.Value.Items);

        _personRepositoryMock.Verify(r => r.GetAllAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllPeopleAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.GetAllPeopleAsync((PersonQueryRequest)null!));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.GetAllPeopleAsync((PaginationRequest)null!));
    }

    [Fact]
    public async Task GetAllPeopleAsync_NoPeople_ReturnsEmptyList()
    {
        // Arrange
        _personRepositoryMock
            .Setup(repository => repository.GetAllPersonsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Person>());

        // Act
        Result<List<PersonDto>> result =
            await _sut.GetAllPeopleAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!);

        _personRepositoryMock.Verify(
            repository => repository.GetAllPersonsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [Fact]
    public async Task GetStatisticsAsync_WhenRepositoryReturnsStats_ReturnsSuccessWithStats()
    {
        var expectedStats = new PersonStatisticsDto
        {
            TotalCount = 15,
            MaleCount = 10,
            FemaleCount = 5
        };

        _personRepositoryMock
            .Setup(r => r.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        var result = await _sut.GetStatisticsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(15, result.Value.TotalCount);
        Assert.Equal(10, result.Value.MaleCount);
        Assert.Equal(5, result.Value.FemaleCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        _personRepositoryMock
            .Setup(r => r.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.GetStatisticsAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal("Failed to retrieve people statistics.", result.Error);
    }

    [Fact]
    public async Task GetPeopleAvailableForUserAsync_WhenValidRequest_ReturnsMappedPagedResult()
    {
        var request = new PersonQueryRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = "Omar"
        };

        var pagedPeople = new PagedResult<Person>
        {
            Items = new List<Person>
            {
                CreatePerson(5, "9998887776")
            },
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _personRepositoryMock
            .Setup(r => r.GetPeopleAvailableForUserAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedPeople);

        var result = await _sut.GetPeopleAvailableForUserAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value!.TotalCount);
        Assert.Single(result.Value.Items);
        Assert.Equal(5, result.Value.Items[0].PersonId);
        _personRepositoryMock.Verify(r => r.GetPeopleAvailableForUserAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPeopleAvailableForUserAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.GetPeopleAvailableForUserAsync(null!));
    }

    [Fact]
    public async Task GetPeopleAvailableForUserAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        var request = new PersonQueryRequest { PageNumber = 1, PageSize = 10 };

        _personRepositoryMock
            .Setup(r => r.GetPeopleAvailableForUserAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.GetPeopleAvailableForUserAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal("Failed to retrieve available people.", result.Error);
    }

    [Fact]
    public async Task GetPersonByIdAsync_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        var person = CreatePerson(10);

        _personRepositoryMock
            .Setup(r => r.GetPersonByIdAsync(10, cts.Token))
            .ReturnsAsync(person);

        var result = await _sut.GetPersonByIdAsync(10, cts.Token);

        Assert.True(result.IsSuccess);
        _personRepositoryMock.Verify(r => r.GetPersonByIdAsync(10, cts.Token), Times.Once);
    }

    [Fact]
    public async Task GetStatisticsAsync_WhenRepositoryThrowsOperationCanceledException_PropagatesWithoutSwallowing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _personRepositoryMock
            .Setup(r => r.GetStatisticsAsync(cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.GetStatisticsAsync(cts.Token));
    }
}
