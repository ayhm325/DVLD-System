using System.Data;

using Application.Common.Results;
using Application.DTOs.UserDTO;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public sealed partial class UserService
{
    public async Task<Result>
        ChangePasswordAsync(
            int userId,
            ChangePasswordDto dto,
            CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.ValidationFailure(
                "Invalid user ID.");
        }

        if (dto is null)
        {
            return Result.ValidationFailure(
                "Change password data is required.");
        }

        var validation =
            UserValidator
                .ValidateChangePassword(dto);

        if (validation.IsFailure)
        {
            return Result.ValidationFailure(
                validation.Error);
        }

        return await _unitOfWork.ExecuteInTransactionAsync(
            async transaction =>
            {
                var user =
                    await _userRepository
                        .GetUserForPasswordChangeAsync(userId, cancellationToken);

                if (user is null)
                {
                    return Result.NotFound(
                        "User not found.");
                }

                var currentPasswordValid =
                    BCrypt.Net.BCrypt
                        .Verify(
                            dto.CurrentPassword,
                            user.Password);

                if (!currentPasswordValid)
                {
                    return Result.ValidationFailure(
                        "Current password is incorrect.");
                }

                user.Password =
                    BCrypt.Net.BCrypt
                        .HashPassword(
                            dto.NewPassword);

                user.SecurityStamp = Guid.NewGuid().ToString("N");

                // Every existing refresh token becomes unusable; the controller issues a
                // fresh session for this device after the password change succeeds.
                await _sessionRepository.RevokeAllForUserAsync(
                    userId,
                    _clock.UtcNow,
                    SessionRevocationReasons.PasswordChanged,
                    cancellationToken);

                return Result.Success();
            },
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }
}
