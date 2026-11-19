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
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock = new();
    private readonly FakeJordanClock _clock = FakeJordanClock.FromUtc(
        new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc));
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sessionRepositoryMock = new Mock<IUserSessionRepository>();

        _transactionMock
            .Setup(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(repository => repository.AcquireAdminMutationLockAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _unitOfWorkMock
            .Setup(unit => unit.ExecuteInTransactionAsync<Result>(
                It.IsAny<Func<IUnitOfWorkTransaction, Task<Result>>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                async (Func<IUnitOfWorkTransaction, Task<Result>> operation,
                    IsolationLevel _,
                    CancellationToken ct) =>
                {
                    try
                    {
                        var result = await operation(_transactionMock.Object);
                        if (result is { IsFailure: true })
                        {
                            await _transactionMock.Object.RollbackAsync(ct);
                            return result;
                        }

                        await _transactionMock.Object.CommitAsync(ct);
                        return result;
                    }
                    catch
                    {
                        await _transactionMock.Object.RollbackAsync(CancellationToken.None);
                        throw;
                    }
                });

        _sut = new UserService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sessionRepositoryMock.Object,
            _clock);
    }

    // =========================================================
    // Test Data Helpers
    // =========================================================

    private static User CreateUser(
        int userId = 10,
        int personId = 20,
        string userName = "ayhm",
        string password = "Password123",
        UserRole role = UserRole.Staff,
        bool isActive = true)
    {
        return new User
        {
            UserId = userId,
            PersonId = personId,
            UserName = userName,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = isActive,
            Role = role,

            Person = new Person
            {
                PersonId = personId,
                FirstName = "Ayhm",
                SecondName = "Mohammed",
                ThirdName = null,
                LastName = "Obeidat"
            }
        };
    }

    private static CreateUserDto CreateValidCreateUserDto()
    {
        return new CreateUserDto
        {
            UserName = "newuser",
            Password = "Password123",
            IsActive = true,
            PersonId = 20
        };
    }

    private static UpdateUserDto CreateValidUpdateUserDto()
    {
        return new UpdateUserDto
        {
            UserName = "updateduser",
            IsActive = false,
            PersonId = 20
        };
    }

    private static ChangePasswordDto CreateValidChangePasswordDto()
    {
        return new ChangePasswordDto
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "NewPassword123"
        };
    }

}
