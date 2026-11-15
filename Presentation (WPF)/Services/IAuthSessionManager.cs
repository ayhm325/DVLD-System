using DVLD.Contracts.Auth;
using Presentation.Services.Results;

namespace Presentation.Services;

public interface IAuthSessionManager
{
    bool IsLoggedIn { get; }

    event EventHandler? SessionExpired;

    Task<ApiResult<LoginResponse>> LoginAsync(
        string username,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default);

    Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default);

    Task<bool> RefreshSessionAsync(CancellationToken cancellationToken = default);

    Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default);

    Task<ApiResult> LogoutAllAsync(CancellationToken cancellationToken = default);

    void ApplySession(LoginResponse loginResponse, bool? rememberMe = null);

    void ClearLocalSession();
}
