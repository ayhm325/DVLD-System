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
    // DeleteUserAsync
    // =========================================================

    [Fact]
    public async Task DeleteUserAsync_InvalidId_ReturnsValidationFailure()
    {
        // Arrange
        const int invalidUserId = 0;

        // Act
        Result result =
            await _sut.DeleteUserAsync(invalidUserId);

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
    public async Task DeleteUserAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        const int userId = 10;

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        Result result =
            await _sut.DeleteUserAsync(userId);

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

        _userRepositoryMock.Verify(
            repository =>
                repository.DeleteUser(
                    It.IsAny<User>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_SaveFails_ReturnsFailure()
    {
        // Arrange
        const int userId = 10;

        var user = CreateUser(userId);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        Result result =
            await _sut.DeleteUserAsync(userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal(
            "Failed to save user deletion.",
            result.Error);

        _userRepositoryMock.Verify(
            repository =>
                repository.DeleteUser(user),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_ValidUser_ReturnsSuccess()
    {
        // Arrange
        const int userId = 10;

        var user = CreateUser(userId);

        _userRepositoryMock
            .Setup(repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result result =
            await _sut.DeleteUserAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);

        _userRepositoryMock.Verify(
            repository =>
                repository.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository =>
                repository.DeleteUser(user),
            Times.Once);

        _unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_PrimaryAdminAccount_CannotBeDeactivated_ReturnsForbidden()
    {
        // Arrange
        const int userId = 2;
        var dto = CreateValidUpdateUserDto();
        dto.IsActive = false;
        var user = CreateUser(userId, role: UserRole.Admin);

        _userRepositoryMock
            .Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal("The primary administrator account cannot be deactivated.", result.Error);
    }

    [Fact]
    public async Task UpdateUserAsync_CurrentAdminAccount_CannotDeactivateSelf_ReturnsForbidden()
    {
        // Arrange
        const int userId = 5;
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(c => c.IsLoggedIn).Returns(true);
        currentUserServiceMock.Setup(c => c.UserId).Returns(userId);
        currentUserServiceMock.Setup(c => c.Role).Returns(UserRole.Admin);

        var service = new UserService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sessionRepositoryMock.Object,
            _clock,
            currentUserServiceMock.Object);

        var dto = CreateValidUpdateUserDto();
        dto.IsActive = false;
        var user = CreateUser(userId, role: UserRole.Admin);

        _userRepositoryMock
            .Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result result = await service.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal("You cannot deactivate your own administrator account.", result.Error);
    }

    [Fact]
    public async Task UpdateUserAsync_LastActiveAdmin_CannotBeDeactivated_ReturnsConflict()
    {
        // Arrange
        const int userId = 99;
        var dto = CreateValidUpdateUserDto();
        dto.IsActive = false;
        var user = CreateUser(userId, role: UserRole.Admin);

        _userRepositoryMock
            .Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.GetActiveAdminCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Cannot deactivate the last active administrator.", result.Error);
    }

    [Fact]
    public async Task DeleteUserAsync_PrimaryAdminAccount_CannotBeDeleted_ReturnsForbidden()
    {
        // Arrange
        const int userId = 2;
        var user = CreateUser(userId, role: UserRole.Admin);

        _userRepositoryMock
            .Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result result = await _sut.DeleteUserAsync(userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal("The primary administrator account cannot be deleted.", result.Error);
    }

    [Fact]
    public async Task DeleteUserAsync_CurrentAdminAccount_CannotDeleteSelf_ReturnsForbidden()
    {
        // Arrange
        const int userId = 7;
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(c => c.IsLoggedIn).Returns(true);
        currentUserServiceMock.Setup(c => c.UserId).Returns(userId);
        currentUserServiceMock.Setup(c => c.Role).Returns(UserRole.Admin);

        var service = new UserService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sessionRepositoryMock.Object,
            _clock,
            currentUserServiceMock.Object);

        var user = CreateUser(userId, role: UserRole.Admin);

        _userRepositoryMock
            .Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result result = await service.DeleteUserAsync(userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal("You cannot delete your own administrator account.", result.Error);
    }

    [Fact]
    public async Task DeleteUserAsync_LastActiveAdmin_CannotBeDeleted_ReturnsConflict()
    {
        // Arrange
        const int userId = 99;
        var user = CreateUser(userId, role: UserRole.Admin);

        _userRepositoryMock
            .Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.GetActiveAdminCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        Result result = await _sut.DeleteUserAsync(userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Cannot delete the last administrator account.", result.Error);
    }

    [Fact]
    public async Task UpdateUserAsync_PrimaryAdminAccount_CannotBeRenamedOrRelinked_ReturnsForbidden()
    {
        // Arrange
        const int userId = 2;
        var user = CreateUser(userId, personId: 100, userName: "ayhm", role: UserRole.Admin);
        var dto = new UpdateUserDto
        {
            PersonId = 100,
            UserName = "renamed_admin",
            IsActive = true
        };

        _userRepositoryMock
            .Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal("The primary administrator account cannot be modified.", result.Error);
    }

    [Fact]
    public async Task AddUserAsync_WhenCallerIsStaff_ReturnsForbidden()
    {
        // Arrange
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(c => c.IsLoggedIn).Returns(true);
        currentUserServiceMock.Setup(c => c.UserId).Returns(15);
        currentUserServiceMock.Setup(c => c.Role).Returns(UserRole.Staff);

        var service = new UserService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sessionRepositoryMock.Object,
            _clock,
            currentUserServiceMock.Object);

        var dto = new CreateUserDto
        {
            PersonId = 200,
            UserName = "escalated_admin",
            Password = "Password123!",
            IsActive = true,
            Role = UserRole.Admin
        };

        // Act
        Result<int> result = await service.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        _userRepositoryMock.Verify(r => r.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserAsync_InvalidRole_ReturnsValidationFailure()
    {
        // Arrange
        var dto = new CreateUserDto
        {
            PersonId = 200,
            UserName = "valid_admin",
            Password = "Password123!",
            IsActive = true,
            Role = (UserRole)99
        };

        // Act
        Result<int> result = await _sut.AddUserAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Invalid user role.", result.Error);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Staff)]
    public async Task AddUserAsync_AdminCaller_PersistsSelectedRole(UserRole selectedRole)
    {
        // Arrange
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(c => c.IsLoggedIn).Returns(true);
        currentUserServiceMock.Setup(c => c.UserId).Returns(2);
        currentUserServiceMock.Setup(c => c.Role).Returns(UserRole.Admin);

        var service = new UserService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sessionRepositoryMock.Object,
            _clock,
            currentUserServiceMock.Object);

        User? addedUser = null;

        _userRepositoryMock
            .Setup(r => r.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) =>
            {
                u.UserId = 501;
                addedUser = u;
            })
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new CreateUserDto
        {
            PersonId = 300,
            UserName = "role_select",
            Password = "Password123!",
            IsActive = true,
            Role = selectedRole
        };

        // Act
        Result<int> result = await service.AddUserAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(501, result.Value);
        Assert.NotNull(addedUser);
        Assert.Equal(selectedRole, addedUser.Role);
        Assert.NotEqual("Password123!", addedUser.Password);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", addedUser.Password));
    }

    [Fact]
    public async Task UpdateUserAsync_AdminPromotesStaffToAdmin_PersistsRole()
    {
        // Arrange
        const int userId = 40;
        var user = CreateUser(userId, personId: 40, userName: "staff_one", role: UserRole.Staff);
        var originalStamp = user.SecurityStamp;

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("staff_one", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = new UpdateUserDto { PersonId = 40, UserName = "staff_one", IsActive = true, Role = UserRole.Admin };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.False(string.IsNullOrWhiteSpace(user.SecurityStamp));

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                userId,
                It.IsAny<DateTime>(),
                SessionRevocationReasons.SecurityStateChanged,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_AdminDemotesAdminWhenOthersExist_PersistsStaffRole()
    {
        // Arrange
        const int userId = 41;
        var user = CreateUser(userId, personId: 41, userName: "second_admin", role: UserRole.Admin);
        var originalStamp = user.SecurityStamp;

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.GetActiveAdminCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("second_admin", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = new UpdateUserDto { PersonId = 41, UserName = "second_admin", IsActive = true, Role = UserRole.Staff };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UserRole.Staff, user.Role);
        Assert.NotEqual(originalStamp, user.SecurityStamp);

        _userRepositoryMock.Verify(
            r => r.AcquireAdminMutationLockAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                userId,
                It.IsAny<DateTime>(),
                SessionRevocationReasons.SecurityStateChanged,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_StaffDeactivation_RotatesSecurityStampRevokesSessionsAndCommitsTransaction()
    {
        // Arrange
        const int userId = 45;
        var user = CreateUser(userId, personId: 45, userName: "staff_deactivate", role: UserRole.Staff, isActive: true);
        var originalStamp = user.SecurityStamp;

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("staff_deactivate", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = new UpdateUserDto { PersonId = 45, UserName = "staff_deactivate", IsActive = false, Role = UserRole.Staff };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
        Assert.NotEqual(originalStamp, user.SecurityStamp);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                userId,
                It.IsAny<DateTime>(),
                SessionRevocationReasons.SecurityStateChanged,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenSecurityUpdateSaveFails_RollsBackTransaction()
    {
        // Arrange
        const int userId = 46;
        var user = CreateUser(userId, personId: 46, userName: "staff_fail_save", role: UserRole.Staff, isActive: true);

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("staff_fail_save", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var dto = new UpdateUserDto { PersonId = 46, UserName = "staff_fail_save", IsActive = false, Role = UserRole.Staff };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Failure, result.ErrorType);
        Assert.Equal("No user changes were saved.", result.Error);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                userId,
                It.IsAny<DateTime>(),
                SessionRevocationReasons.SecurityStateChanged,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenSecurityUpdateThrows_RollsBackTransactionAndPropagates()
    {
        // Arrange
        const int userId = 47;
        var user = CreateUser(userId, personId: 47, userName: "staff_throw", role: UserRole.Staff, isActive: true);

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("staff_throw", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated database failure."));

        var dto = new UpdateUserDto { PersonId = 47, UserName = "staff_throw", IsActive = false, Role = UserRole.Staff };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateUserAsync(userId, dto));

        Assert.Equal("Simulated database failure.", ex.Message);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                userId,
                It.IsAny<DateTime>(),
                SessionRevocationReasons.SecurityStateChanged,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_NormalProfileUpdate_DoesNotRevokeSessionsOrUseTransaction()
    {
        // Arrange
        const int userId = 48;
        var user = CreateUser(userId, personId: 20, userName: "staff_old_name", role: UserRole.Staff, isActive: true);
        var originalStamp = user.SecurityStamp;

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("staff_new_name", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = new UpdateUserDto { PersonId = 20, UserName = "staff_new_name", IsActive = true, Role = UserRole.Staff };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("staff_new_name", user.UserName);
        Assert.Equal(originalStamp, user.SecurityStamp);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync<Result>(
                It.IsAny<Func<IUnitOfWorkTransaction, Task<Result>>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_StaffUser_RevokesSessionsAndDeletesUnderTransaction()
    {
        // Arrange
        const int userId = 49;
        var user = CreateUser(userId, role: UserRole.Staff);

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        Result result = await _sut.DeleteUserAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);

        _userRepositoryMock.Verify(
            r => r.DeleteUser(user),
            Times.Once);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                userId,
                It.IsAny<DateTime>(),
                SessionRevocationReasons.SecurityStateChanged,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_StaffUser_WhenSaveFails_RollsBackTransaction()
    {
        // Arrange
        const int userId = 50;
        var user = CreateUser(userId, role: UserRole.Staff);

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        // Act
        Result result = await _sut.DeleteUserAsync(userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to save user deletion.", result.Error);

        _userRepositoryMock.Verify(
            r => r.DeleteUser(user),
            Times.Once);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                userId,
                It.IsAny<DateTime>(),
                SessionRevocationReasons.SecurityStateChanged,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_AdminDemotingAdmin_WhenMutationLockFails_RollsBackTransaction()
    {
        // Arrange
        const int userId = 51;
        var user = CreateUser(userId, personId: 51, userName: "admin_locked", role: UserRole.Admin, isActive: true);

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.GetActiveAdminCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("admin_locked", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _userRepositoryMock.Setup(r => r.AcquireAdminMutationLockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var dto = new UpdateUserDto { PersonId = 51, UserName = "admin_locked", IsActive = true, Role = UserRole.Staff };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Another administrator change is in progress. Please retry.", result.Error);

        _userRepositoryMock.Verify(
            r => r.AcquireAdminMutationLockAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _sessionRepositoryMock.Verify(
            s => s.RevokeAllForUserAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _transactionMock.Verify(
            t => t.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_NullRole_KeepsExistingRole()
    {
        // Arrange
        const int userId = 42;
        var user = CreateUser(userId, personId: 42, userName: "keeps_role", role: UserRole.Admin);

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.IsUsernameTakenForAnotherUserAsync("renamed_keep", userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = new UpdateUserDto { PersonId = 42, UserName = "renamed_keep", IsActive = true, Role = null };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UserRole.Admin, user.Role);
        _userRepositoryMock.Verify(r => r.GetActiveAdminCountAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_SelfDemotion_ReturnsForbidden()
    {
        // Arrange
        const int userId = 43;
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(c => c.IsLoggedIn).Returns(true);
        currentUserServiceMock.Setup(c => c.UserId).Returns(userId);
        currentUserServiceMock.Setup(c => c.Role).Returns(UserRole.Admin);

        var service = new UserService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sessionRepositoryMock.Object,
            _clock,
            currentUserServiceMock.Object);

        var user = CreateUser(userId, personId: 43, userName: "self_admin", role: UserRole.Admin);
        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.GetActiveAdminCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var dto = new UpdateUserDto { PersonId = 43, UserName = "self_admin", IsActive = true, Role = UserRole.Staff };

        // Act
        Result result = await service.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal("You cannot change your own role.", result.Error);
        Assert.Equal(UserRole.Admin, user.Role);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_DemotingLastActiveAdmin_ReturnsConflict()
    {
        // Arrange
        const int userId = 44;
        var user = CreateUser(userId, personId: 44, userName: "only_admin", role: UserRole.Admin);

        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.GetActiveAdminCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = new UpdateUserDto { PersonId = 44, UserName = "only_admin", IsActive = true, Role = UserRole.Staff };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Cannot change the role of the last active administrator.", result.Error);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public async Task UpdateUserAsync_PrimaryAdminRoleChange_ReturnsForbidden()
    {
        // Arrange
        const int userId = 2;
        var user = CreateUser(userId, personId: 100, userName: "ayhm", role: UserRole.Admin);
        _userRepositoryMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var dto = new UpdateUserDto { PersonId = 100, UserName = "ayhm", IsActive = true, Role = UserRole.Staff };

        // Act
        Result result = await _sut.UpdateUserAsync(userId, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal("The primary administrator account role cannot be changed.", result.Error);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public async Task UpdateUserAsync_StaffCallerAttemptsRoleChange_ReturnsForbidden()
    {
        // Arrange
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(c => c.IsLoggedIn).Returns(true);
        currentUserServiceMock.Setup(c => c.UserId).Returns(15);
        currentUserServiceMock.Setup(c => c.Role).Returns(UserRole.Staff);

        var service = new UserService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sessionRepositoryMock.Object,
            _clock,
            currentUserServiceMock.Object);

        var dto = new UpdateUserDto { PersonId = 15, UserName = "staff_self", IsActive = true, Role = UserRole.Admin };

        // Act
        Result result = await service.UpdateUserAsync(15, dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        _userRepositoryMock.Verify(r => r.GetUserForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_InvalidRole_ReturnsValidationFailure()
    {
        var dto = new UpdateUserDto { PersonId = 15, UserName = "valid_name", IsActive = true, Role = (UserRole)9 };

        Result result = await _sut.UpdateUserAsync(15, dto);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Invalid user role.", result.Error);
    }

}
