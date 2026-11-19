using System.Data;

using Application.Common.Exceptions;
using Application.Common.Results;
using Application.DTOs.UserDTO;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Common;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.UnitTests.Services;

public partial class UserServiceTests
{
    // =========================================================
    // GetUserByIdAsync
    // =========================================================

    [Fact]
    public async Task GetUserByIdAsync_InvalidId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidUserId = 0;

        // Act
        Result<UserDto> result =
            await _sut.GetUserByIdAsync(invalidUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Invalid user ID.", result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByUserIdAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUserByIdAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        const int userId = 10;

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        Result<UserDto> result =
            await _sut.GetUserByIdAsync(userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal("User not found.", result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserByIdAsync_UserExists_ReturnsSuccess()
    {
        // Arrange
        const int userId = 10;

        var user = CreateUser(userId);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result<UserDto> result =
            await _sut.GetUserByIdAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.NotNull(result.Value);

        Assert.Equal(user.UserId, result.Value!.UserId);
        Assert.Equal(user.PersonId, result.Value.PersonId);
        Assert.Equal(user.UserName, result.Value.UserName);
        Assert.Equal(user.IsActive, result.Value.IsActive);
        Assert.Equal(user.Role, result.Value.Role);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // GetUserByPersonIdAsync
    // =========================================================

    [Fact]
    public async Task GetUserByPersonIdAsync_InvalidId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidPersonId = 0;

        // Act
        Result<UserDto> result =
            await _sut.GetUserByPersonIdAsync(
                invalidPersonId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Invalid person ID.", result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByPersonIdAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUserByPersonIdAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        const int personId = 20;

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserByPersonIdAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        Result<UserDto> result =
            await _sut.GetUserByPersonIdAsync(personId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "No user is associated with this person.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByPersonIdAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserByPersonIdAsync_UserExists_ReturnsSuccess()
    {
        // Arrange
        const int personId = 20;

        var user = CreateUser(
            userId: 10,
            personId: personId);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserByPersonIdAsync(personId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result<UserDto> result =
            await _sut.GetUserByPersonIdAsync(personId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(personId, result.Value!.PersonId);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByPersonIdAsync(personId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentProfileAsync_WhenUserHasPersonWithRowVersion_ReturnsProfileWithExactBase64RowVersion()
    {
        // Arrange
        const int userId = 10;
        const int personId = 20;
        byte[] expectedRowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        string expectedBase64 = Convert.ToBase64String(expectedRowVersion);

        var user = CreateUser(userId: userId, personId: personId);
        user.Person!.RowVersion = (byte[])expectedRowVersion.Clone();
        user.Person.NationalNo = "N123456";
        user.Person.Address = "Test Address";
        user.Person.Phone = "0791234567";

        _userRepositoryMock
            .Setup(r => r.GetUserProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetCurrentProfileAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(userId, result.Value!.UserId);
        Assert.Equal(personId, result.Value.PersonId);
        Assert.Equal(expectedBase64, result.Value.RowVersion);
        Assert.Equal("N123456", result.Value.NationalNo);
    }

    // =========================================================
    // GetUserByUsernameAsync
    // =========================================================

    [Fact]
    public async Task GetUserByUsernameAsync_EmptyUsername_ReturnsValidationFailure()
    {
        // Arrange
        const string username = "   ";

        // Act
        Result<UserDto> result =
            await _sut.GetUserByUsernameAsync(username);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Username is required.", result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByUsernameAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        const string username = "ayhm";

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        Result<UserDto> result =
            await _sut.GetUserByUsernameAsync(username);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal("User not found.", result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByUsernameAsync(username, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_UsernameHasWhitespace_TrimsBeforeRepositoryCall()
    {
        // Arrange
        const string inputUsername = "  ayhm  ";
        const string normalizedUsername = "ayhm";

        var user = CreateUser(
            userId: 10,
            userName: normalizedUsername);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserByUsernameAsync(
                    normalizedUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result<UserDto> result =
            await _sut.GetUserByUsernameAsync(
                inputUsername);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(
            normalizedUsername,
            result.Value!.UserName);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByUsernameAsync(
                    normalizedUsername, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_UserExists_ReturnsSuccess()
    {
        // Arrange
        const string username = "ayhm";

        var user = CreateUser(
            userId: 10,
            userName: username);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result<UserDto> result =
            await _sut.GetUserByUsernameAsync(username);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.NotNull(result.Value);
        Assert.Equal(username, result.Value!.UserName);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserByUsernameAsync(username, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // IsUsernameTakenForAnotherUserAsync
    // =========================================================

    [Fact]
    public async Task IsUsernameTakenForAnotherUserAsync_InvalidInput_ReturnsFalse()
    {
        // Arrange
        const string username = "ayhm";

        // Act
        bool result =
            await _sut.IsUsernameTakenForAnotherUserAsync(
                username,
                0);

        // Assert
        Assert.False(result);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IsUsernameTakenForAnotherUserAsync_ValidInput_DelegatesToRepository()
    {
        // Arrange
        const string username = "ayhm";
        const int userId = 10;

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    username,
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        bool result =
            await _sut.IsUsernameTakenForAnotherUserAsync(
                username,
                userId);

        // Assert
        Assert.True(result);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    username,
                    userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsUsernameTakenForAnotherUserAsync_UsernameHasWhitespace_TrimsBeforeRepositoryCall()
    {
        // Arrange
        const string inputUsername = "  ayhm  ";
        const string normalizedUsername = "ayhm";
        const int userId = 10;

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    normalizedUsername,
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        bool result =
            await _sut.IsUsernameTakenForAnotherUserAsync(
                inputUsername,
                userId);

        // Assert
        Assert.True(result);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    normalizedUsername,
                    userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // GetAllUsersAsync
    // =========================================================

    [Fact]
    public async Task GetAllUsersAsync_UsersExist_ReturnsMappedUsers()
    {
        // Arrange
        var users = new List<User>
        {
            CreateUser(1, 10, "ayhm1"),
            CreateUser(2, 20, "ayhm2")
        };

        _userRepositoryMock
            .Setup(repository =>
                repository.GetAllUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        Result<List<UserDto>> result =
            await _sut.GetAllUsersAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.NotNull(result.Value);

        Assert.Equal(2, result.Value!.Count);

        Assert.Equal(
            users[0].UserId,
            result.Value[0].UserId);

        Assert.Equal(
            users[0].UserName,
            result.Value[0].UserName);

        Assert.Equal(
            users[1].UserId,
            result.Value[1].UserId);

        Assert.Equal(
            users[1].UserName,
            result.Value[1].UserName);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetAllUsersAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUsersAsync_NoUsers_ReturnsEmptyList()
    {
        // Arrange
        _userRepositoryMock
            .Setup(repository =>
                repository.GetAllUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        Result<List<UserDto>> result =
            await _sut.GetAllUsersAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetAllUsersAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // =========================================================
    // IsUserActiveAsync
    // =========================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IsUserActiveAsync_InvalidUserId_ReturnsFalseWithoutCallingRepository(
        int invalidUserId)
    {
        bool result = await _sut.IsUserActiveAsync(invalidUserId);

        Assert.False(result);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUserActiveAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsUserActiveAsync_ValidUserId_ReturnsRepositoryResult(
        bool expectedActive)
    {
        const int userId = 10;

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserActiveAsync(
                    userId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedActive);

        bool result = await _sut.IsUserActiveAsync(userId);

        Assert.Equal(expectedActive, result);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUserActiveAsync(
                    userId,
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUsersAsync_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        _userRepositoryMock
            .Setup(r => r.GetAllUsersAsync(cts.Token))
            .ReturnsAsync(new List<User>());

        await _sut.GetAllUsersAsync(cts.Token);

        _userRepositoryMock.Verify(
            r => r.GetAllUsersAsync(cts.Token),
            Times.Once);
    }
}
