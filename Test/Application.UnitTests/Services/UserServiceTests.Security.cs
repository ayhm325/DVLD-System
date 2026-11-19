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
    // ChangePasswordAsync
    // =========================================================

    [Fact]
    public async Task ChangePasswordAsync_InvalidUserId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidUserId = 0;

        var dto = new ChangePasswordDto
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "NewPassword123"
        };

        // Act
        Result result =
            await _sut.ChangePasswordAsync(
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
    public async Task ChangePasswordAsync_NullDto_ReturnsValidationFailure()
    {
        // Arrange
        const int userId = 10;
        ChangePasswordDto? dto = null;

        // Act
        Result result =
            await _sut.ChangePasswordAsync(
                userId,
                dto!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Change password data is required.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForPasswordChangeAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_InvalidDto_ReturnsValidationFailure()
    {
        // Arrange
        const int userId = 10;

        var dto = new ChangePasswordDto
        {
            CurrentPassword = "",
            NewPassword = "123"
        };

        // Act
        Result result =
            await _sut.ChangePasswordAsync(
                userId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForPasswordChangeAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

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
    public async Task ChangePasswordAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        const int userId = 10;

        var dto = CreateValidChangePasswordDto();

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        Result result =
            await _sut.ChangePasswordAsync(
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
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _sessionRepositoryMock.Verify(
            sessionRepo =>
                sessionRepo.RevokeAllForUserAsync(
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsValidationFailure()
    {
        // Arrange
        const int userId = 10;

        const string currentPassword = "CorrectPassword123";
        const string wrongPassword = "WrongPassword123";

        var dto = new ChangePasswordDto
        {
            CurrentPassword = wrongPassword,
            NewPassword = "NewPassword123"
        };

        var user = CreateUser(
            userId,
            password: currentPassword);
        var originalPasswordHash = user.Password;
        var originalSecurityStamp = user.SecurityStamp;

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result result =
            await _sut.ChangePasswordAsync(
                userId,
                dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Current password is incorrect.",
            result.Error);
        Assert.Equal(originalPasswordHash, user.Password);
        Assert.Equal(originalSecurityStamp, user.SecurityStamp);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _sessionRepositoryMock.Verify(
            sessionRepo =>
                sessionRepo.RevokeAllForUserAsync(
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidData_ReturnsSuccessAndUpdatesPassword()
    {
        // Arrange
        const int userId = 10;

        var dto = CreateValidChangePasswordDto();

        var user = CreateUser(
            userId,
            password: dto.CurrentPassword);

        var oldHash = user.Password;
        var oldSecurityStamp = user.SecurityStamp;

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result result =
            await _sut.ChangePasswordAsync(
                userId,
                dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);

        Assert.NotEqual(oldHash, user.Password);
        Assert.NotEqual(oldSecurityStamp, user.SecurityStamp);
        Assert.False(string.IsNullOrWhiteSpace(user.SecurityStamp));

        Assert.True(
            BCrypt.Net.BCrypt.Verify(
                dto.NewPassword,
                user.Password));

        Assert.False(
            BCrypt.Net.BCrypt.Verify(
                dto.CurrentPassword,
                user.Password));

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _sessionRepositoryMock.Verify(
            sessionRepo =>
                sessionRepo.RevokeAllForUserAsync(
                    userId,
                    It.IsAny<DateTime>(),
                    SessionRevocationReasons.PasswordChanged,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            transaction =>
                transaction.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            transaction =>
                transaction.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenSessionRevocationThrows_RollsBackTransactionAndPropagates()
    {
        // Arrange
        const int userId = 10;
        var dto = CreateValidChangePasswordDto();
        var user = CreateUser(userId, password: dto.CurrentPassword);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _sessionRepositoryMock
            .Setup(sessionRepo =>
                sessionRepo.RevokeAllForUserAsync(
                    userId,
                    It.IsAny<DateTime>(),
                    SessionRevocationReasons.PasswordChanged,
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Session revocation failed."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ChangePasswordAsync(userId, dto));

        Assert.Equal("Session revocation failed.", ex.Message);

        _transactionMock.Verify(
            transaction =>
                transaction.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            transaction =>
                transaction.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCommitThrows_RollsBackTransactionAndPropagates()
    {
        // Arrange
        const int userId = 10;
        var dto = CreateValidChangePasswordDto();
        var user = CreateUser(userId, password: dto.CurrentPassword);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForPasswordChangeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _transactionMock
            .Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database write failure."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ChangePasswordAsync(userId, dto));

        Assert.Equal("Database write failure.", ex.Message);

        _transactionMock.Verify(
            transaction =>
                transaction.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            transaction =>
                transaction.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCancelledDuringCommit_RollsBackTransactionAndPropagates()
    {
        // Arrange
        const int userId = 10;
        var dto = CreateValidChangePasswordDto();
        var user = CreateUser(userId, password: dto.CurrentPassword);

        using var cts = new CancellationTokenSource();

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForPasswordChangeAsync(userId, cts.Token))
            .ReturnsAsync(user);

        _sessionRepositoryMock
            .Setup(sessionRepo =>
                sessionRepo.RevokeAllForUserAsync(
                    userId,
                    It.IsAny<DateTime>(),
                    SessionRevocationReasons.PasswordChanged,
                    cts.Token))
            .ReturnsAsync(1);

        _transactionMock
            .Setup(t => t.CommitAsync(cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ChangePasswordAsync(userId, dto, cts.Token));

        _transactionMock.Verify(
            transaction =>
                transaction.RollbackAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            transaction =>
                transaction.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCancelled_PropagatesCancellationWithoutCommit()
    {
        // Arrange
        const int userId = 10;
        var dto = CreateValidChangePasswordDto();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForPasswordChangeAsync(userId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ChangePasswordAsync(userId, dto, cts.Token));

        _transactionMock.Verify(
            transaction =>
                transaction.CommitAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
