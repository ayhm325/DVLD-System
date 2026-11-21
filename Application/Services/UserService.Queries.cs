using Application.Common.Results;
using Application.DTOs.UserDTO;
using Application.Mappers;

namespace Application.Services;

public sealed partial class UserService
{
    public async Task<Result<List<UserDto>>>
        GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        if (IsNonAdminAuthenticatedCaller())
        {
            return Result<List<UserDto>>.FromForbidden(
                "Administrator privileges are required.");
        }

        var users =
            await _userRepository
                .GetAllUsersAsync(cancellationToken);

        var dtos =
            users
                .Select(UserMapper.ToDto)
                .ToList();

        return Result<List<UserDto>>
            .Success(dtos);
    }

    public async Task<Result<UserDto>>
        GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<UserDto>
                .FromValidationFailure(
                    "Invalid user ID.");
        }

        var user =
            await _userRepository
                .GetUserByUserIdAsync(id, cancellationToken);

        if (user is null)
        {
            return Result<UserDto>
                .FromNotFound(
                    "User not found.");
        }

        return Result<UserDto>
            .Success(
                UserMapper.ToDto(user));
    }

    public async Task<Result<UserDetailsDto>> GetUserDetailsAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (IsNonAdminAuthenticatedCaller())
        {
            return Result<UserDetailsDto>.FromForbidden(
                "Administrator privileges are required.");
        }

        if (userId <= 0)
        {
            return Result<UserDetailsDto>.FromValidationFailure(
                "User ID must be greater than zero.");
        }

        var user = await _userRepository
            .GetUserDetailsByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return Result<UserDetailsDto>.FromNotFound(
                $"User with ID {userId} was not found.");
        }

        return Result<UserDetailsDto>.Success(user);
    }

    public async Task<Result<UserProfileDto>>
        GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result<UserProfileDto>
                .FromValidationFailure(
                    "Invalid user ID.");
        }

        var user =
            await _userRepository
                .GetUserProfileAsync(userId, cancellationToken);

        if (user is null)
        {
            return Result<UserProfileDto>
                .FromNotFound(
                    "User not found.");
        }

        return Result<UserProfileDto>
            .Success(
                UserMapper.ToProfileDto(user));
    }

    public async Task<Result<UserDto>>
        GetUserByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (personId <= 0)
        {
            return Result<UserDto>
                .FromValidationFailure(
                    "Invalid person ID.");
        }

        var user =
            await _userRepository
                .GetUserByPersonIdAsync(personId, cancellationToken);

        if (user is null)
        {
            return Result<UserDto>
                .FromNotFound(
                    "No user is associated with this person.");
        }

        return Result<UserDto>
            .Success(
                UserMapper.ToDto(user));
    }

    public async Task<Result<UserDto>>
        GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Result<UserDto>
                .FromValidationFailure(
                    "Username is required.");
        }

        var normalizedUsername =
            username.Trim();

        var user =
            await _userRepository
                .GetUserByUsernameAsync(
                    normalizedUsername,
                    cancellationToken);

        if (user is null)
        {
            return Result<UserDto>
                .FromNotFound(
                    "User not found.");
        }

        return Result<UserDto>
            .Success(
                UserMapper.ToDto(user));
    }

    public Task<bool>
        IsUsernameTakenForAnotherUserAsync(
            string username,
            int userId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            userId <= 0)
        {
            return Task.FromResult(false);
        }

        return _userRepository
            .IsUsernameTakenForAnotherUserAsync(
                username.Trim(),
                userId,
                cancellationToken);
    }

    public Task<bool> IsUserActiveAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return Task.FromResult(false);

        return _userRepository.IsUserActiveAsync(
            userId,
            cancellationToken);
    }
}
