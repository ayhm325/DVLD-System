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
    // AddUserAsync
    // =========================================================

    [Fact]
    public async Task AddUserAsync_NullDto_ReturnsValidationFailure()
    {
        // Arrange
        CreateUserDto? dto = null;

        // Act
        Result<int> result =
            await _sut.AddUserAsync(dto!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "User data is required.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserAsync_InvalidDto_ReturnsValidationFailure()
    {
        // Arrange
        var dto = new CreateUserDto
        {
            UserName = "a",
            Password = "123",
            IsActive = true,
            PersonId = 0
        };

        // Act
        Result<int> result =
            await _sut.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserAsync_DuplicateUsername_ReturnsConflict()
    {
        // Arrange
        var dto = CreateValidCreateUserDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenAsync(
                    dto.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result<int> result =
            await _sut.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "Username is already in use.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenAsync(
                    dto.UserName, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUserExistsByPersonIdAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserAsync_PersonAlreadyHasUser_ReturnsConflict()
    {
        // Arrange
        var dto = CreateValidCreateUserDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenAsync(
                    dto.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result<int> result =
            await _sut.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "This person is already associated with a user account.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenAsync(
                    dto.UserName, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserAsync_SaveFails_ReturnsFailure()
    {
        // Arrange
        var dto = CreateValidCreateUserDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenAsync(
                    dto.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) =>
                user.UserId = 1)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        Result<int> result =
            await _sut.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to create user.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddUserAsync_WhenSaveThrowsDuplicateUsernameException_ReturnsConflict()
    {
        // Arrange
        var dto = CreateValidCreateUserDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenAsync(dto.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateUsernameException());

        // Act
        Result<int> result = await _sut.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Username is already in use.", result.Error);

        _userRepositoryMock.Verify(
            repository => repository.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddUserAsync_WhenSaveThrowsDuplicatePersonUserException_ReturnsConflict()
    {
        // Arrange
        var dto = CreateValidCreateUserDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenAsync(dto.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicatePersonUserException());

        // Act
        Result<int> result = await _sut.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("This person is already associated with a user account.", result.Error);

        _userRepositoryMock.Verify(
            repository => repository.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddUserAsync_WhenSaveThrowsUnrelatedException_PropagatesException()
    {
        // Arrange
        var dto = CreateValidCreateUserDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenAsync(dto.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unrelated database error."));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddUserAsync(dto));

        _userRepositoryMock.Verify(
            repository => repository.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddUserAsync_ValidDto_ReturnsCreatedUserIdAndHashesPassword()
    {
        // Arrange
        var dto = CreateValidCreateUserDto();

        User? addedUser = null;

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenAsync(
                    dto.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) =>
            {
                addedUser = user;
                user.UserId = 25;
            })
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result<int> result =
            await _sut.AddUserAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Equal(25, result.Value);

        Assert.NotNull(addedUser);
        Assert.NotEqual(dto.Password, addedUser!.Password);
        Assert.True(
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                addedUser.Password));

        Assert.Equal(
            dto.UserName,
            addedUser.UserName);

        Assert.Equal(
            dto.PersonId,
            addedUser.PersonId);

        Assert.Equal(
            dto.IsActive,
            addedUser.IsActive);

        Assert.Equal(
            UserRole.Staff,
            addedUser.Role);

        _userRepositoryMock.Verify(
            repository =>
                repository.AddUserAsync(
                    It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================
    // UpdateUserAsync
    // =========================================================

    [Fact]
    public async Task UpdateUserAsync_InvalidId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidUserId = 0;
        var dto = CreateValidUpdateUserDto();

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                invalidUserId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Invalid user ID.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_NullDto_ReturnsValidationFailure()
    {
        // Arrange
        const int userId = 10;
        UpdateUserDto? dto = null;

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "User data is required.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_InvalidDto_ReturnsValidationFailure()
    {
        // Arrange
        const int userId = 10;

        var dto = new UpdateUserDto
        {
            UserName = "a",
            IsActive = true,
            PersonId = 0
        };

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        const int userId = 10;
        var dto = CreateValidUpdateUserDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "User not found.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_DuplicateUsername_ReturnsConflict()
    {
        // Arrange
        const int userId = 10;

        var dto = CreateValidUpdateUserDto();

        var user = CreateUser(
            userId,
            personId: 20);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    dto.UserName,
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "Username is already in use by another user.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    dto.UserName,
                    userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUserExistsByPersonIdAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_PersonAlreadyHasAnotherUser_ReturnsConflict()
    {
        // Arrange
        const int userId = 10;

        var dto = CreateValidUpdateUserDto();
        dto.PersonId = 30;

        var user = CreateUser(
            userId,
            personId: 20);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    dto.UserName,
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "This person is already associated with another user account.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_SaveFails_ReturnsFailure()
    {
        // Arrange
        const int userId = 10;

        var dto = CreateValidUpdateUserDto();

        var user = CreateUser(
            userId,
            personId: dto.PersonId);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    dto.UserName,
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "No user changes were saved.",
            result.Error);

        Assert.Equal(
            dto.UserName,
            user.UserName);

        Assert.Equal(
            dto.IsActive,
            user.IsActive);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_ValidDto_ReturnsSuccess()
    {
        // Arrange
        const int userId = 10;

        var dto = CreateValidUpdateUserDto();
        dto.PersonId = 30;

        var user = CreateUser(
            userId,
            personId: 20);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    dto.UserName,
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);

        Assert.Equal(
            dto.UserName,
            user.UserName);

        Assert.Equal(
            dto.PersonId,
            user.PersonId);

        Assert.Equal(
            dto.IsActive,
            user.IsActive);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    dto.UserName,
                    userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.IsUserExistsByPersonIdAsync(
                    dto.PersonId, It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenNoPropertiesChanged_ReturnsSuccessWithoutSaving()
    {
        // Arrange
        const int userId = 10;

        var user = CreateUser(
            userId,
            personId: 20,
            userName: "ayhm");

        var dto = new UpdateUserDto
        {
            UserName = "  ayhm  ",
            PersonId = 20,
            IsActive = user.IsActive
        };

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    "ayhm",
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        Result result =
            await _sut.UpdateUserAsync(
                userId,
                dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_SaveThrowsDbUpdateException_PropagatesException()
    {
        // Arrange
        const int userId = 10;

        var dto = CreateValidUpdateUserDto();

        var user = CreateUser(
            userId,
            personId: dto.PersonId);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository =>
                repository.IsUsernameTakenForAnotherUserAsync(
                    dto.UserName,
                    userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new Microsoft.EntityFrameworkCore.DbUpdateException(
                    "Simulated user persistence exception."));

        // Act & Assert
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => _sut.UpdateUserAsync(
                userId,
                dto));
    }

}
