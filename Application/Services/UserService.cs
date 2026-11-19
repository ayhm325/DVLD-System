using Application.Interfaces;
using Domain.Enums;

namespace Application.Services;

public sealed partial class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IJordanClock _clock;
    private readonly ICurrentUserService? _currentUserService;

    public UserService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IUserSessionRepository sessionRepository,
        IJordanClock clock,
        ICurrentUserService? currentUserService = null)
    {
        _userRepository =
            userRepository
            ?? throw new ArgumentNullException(
                nameof(userRepository));

        _unitOfWork =
            unitOfWork
            ?? throw new ArgumentNullException(
                nameof(unitOfWork));

        _sessionRepository =
            sessionRepository
            ?? throw new ArgumentNullException(
                nameof(sessionRepository));

        _clock =
            clock
            ?? throw new ArgumentNullException(
                nameof(clock));

        _currentUserService = currentUserService;
    }

    private bool IsNonAdminAuthenticatedCaller() =>
        _currentUserService is { IsLoggedIn: true } &&
        _currentUserService.Role != UserRole.Admin;
}
