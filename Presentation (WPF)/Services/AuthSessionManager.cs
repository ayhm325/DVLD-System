using DVLD.Contracts.Auth;
using Presentation.Services.Results;
using System.Net;

namespace Presentation.Services;

public sealed class AuthSessionManager : IAuthSessionManager, IDisposable
{
    private static readonly Uri BaseApiUri = new("http://localhost:5260/");
    private static readonly Uri AuthApiUri = new("http://localhost:5260/api/Auth");
    private static readonly Uri AuthApiLowerUri = new("http://localhost:5260/api/auth");

    private readonly IAuthApiClient _authApiClient;
    private readonly ICurrentUserSession _currentUser;
    private readonly ISecureCredentialStorage _secureStorage;
    private readonly CookieContainer _cookieContainer;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private Timer? _renewalTimer;
    private int _consecutiveTransientFailures;
    private bool _isDisposed;

    public event EventHandler? SessionExpired;

    public bool IsLoggedIn => _currentUser.IsLoggedIn;

    public AuthSessionManager(
        IAuthApiClient authApiClient,
        ICurrentUserSession currentUser,
        ISecureCredentialStorage secureStorage,
        CookieContainer cookieContainer)
    {
        _authApiClient = authApiClient ?? throw new ArgumentNullException(nameof(authApiClient));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        _cookieContainer = cookieContainer ?? throw new ArgumentNullException(nameof(cookieContainer));
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(
        string username,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var result = await _authApiClient.LoginAsync(
            new LoginRequest
            {
                UserName = username.Trim(),
                Password = password
            }, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            ApplySession(result.Value, rememberMe);
        }

        return result;
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_secureStorage.HasStoredCredential())
        {
            return false;
        }

        var storedRefreshToken = _secureStorage.LoadRefreshToken();
        if (string.IsNullOrWhiteSpace(storedRefreshToken))
        {
            _secureStorage.Clear();
            return false;
        }

        InjectRefreshTokenCookie(storedRefreshToken);

        var refreshed = await RefreshSessionAsync(cancellationToken);
        if (!refreshed)
        {
            // If failed definitively, storage was cleared by RefreshSessionAsync.
            return false;
        }

        return true;
    }

    public async Task<bool> RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Check if another concurrent thread already refreshed the session recently
            if (_currentUser.IsLoggedIn &&
                _currentUser.ExpiresAtUtc.HasValue &&
                _currentUser.ExpiresAtUtc.Value > DateTime.UtcNow.AddMinutes(2))
            {
                return true;
            }

            var result = await _authApiClient.RefreshAsync(cancellationToken);

            // Handle 409 Conflict: concurrent refresh race within rotation grace window
            if (result.StatusCode == HttpStatusCode.Conflict)
            {
                await Task.Delay(250, cancellationToken);
                result = await _authApiClient.RefreshAsync(cancellationToken);
            }

            if (result.IsSuccess && result.Value is not null)
            {
                _consecutiveTransientFailures = 0;
                ApplySession(result.Value);
                return true;
            }

            // If 401 Unauthorized, session is dead and cannot be recovered
            if (result.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearLocalSession();
                SessionExpired?.Invoke(this, EventArgs.Empty);
                return false;
            }

            // For transient errors (network down, 500, timeout), do not destroy the session
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _authApiClient.LogoutAsync(cancellationToken);
        }
        finally
        {
            ClearLocalSession();
        }
    }

    public async Task<ApiResult> LogoutAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _authApiClient.LogoutAllAsync(cancellationToken);
        }
        finally
        {
            ClearLocalSession();
        }
    }

    public void ApplySession(LoginResponse loginResponse, bool? rememberMe = null)
    {
        ArgumentNullException.ThrowIfNull(loginResponse);

        _currentUser.SetSession(
            loginResponse.UserId,
            loginResponse.PersonId,
            loginResponse.UserName,
            loginResponse.FullName,
            loginResponse.Role,
            loginResponse.AccessToken,
            loginResponse.ExpiresAtUtc);

        var shouldRemember = rememberMe ?? Properties.Settings.Default.RememberMe;

        if (shouldRemember)
        {
            var refreshToken = ExtractRefreshTokenFromCookieContainer();
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                _secureStorage.SaveRefreshToken(refreshToken);
            }
        }
        else if (rememberMe.HasValue && !rememberMe.Value)
        {
            _secureStorage.Clear();
        }

        ScheduleProactiveRenewal(loginResponse.ExpiresAtUtc);
    }

    public void ClearLocalSession()
    {
        StopRenewalTimer();
        _currentUser.Clear();
        _secureStorage.Clear();
        ClearCookies();
    }

    private void ScheduleProactiveRenewal(DateTime? expiresAtUtc)
    {
        StopRenewalTimer();

        if (!IsLoggedIn)
            return;

        TimeSpan delay;
        if (expiresAtUtc.HasValue)
        {
            var leadTime = TimeSpan.FromMinutes(2);
            var remaining = expiresAtUtc.Value - DateTime.UtcNow;
            delay = remaining > leadTime ? remaining - leadTime : TimeSpan.FromSeconds(5);
        }
        else
        {
            delay = TimeSpan.FromMinutes(13); // fallback for 15m token
        }

        if (delay < TimeSpan.FromSeconds(5))
            delay = TimeSpan.FromSeconds(5);

        _renewalTimer = new Timer(async _ =>
        {
            try
            {
                var success = await RefreshSessionAsync(CancellationToken.None);
                if (!success && IsLoggedIn)
                {
                    // Transient retry with bounded exponential backoff: 15s -> 30s -> 60s
                    _consecutiveTransientFailures++;
                    var backoffSeconds = _consecutiveTransientFailures switch
                    {
                        1 => 15,
                        2 => 30,
                        _ => 60
                    };

                    _renewalTimer?.Change(TimeSpan.FromSeconds(backoffSeconds), Timeout.InfiniteTimeSpan);
                }
            }
            catch
            {
                // Unhandled timer callback exceptions must not crash process
            }
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private void StopRenewalTimer()
    {
        _renewalTimer?.Dispose();
        _renewalTimer = null;
    }

    private string? ExtractRefreshTokenFromCookieContainer()
    {
        foreach (var uri in new[] { AuthApiUri, AuthApiLowerUri, BaseApiUri })
        {
            var cookies = _cookieContainer.GetCookies(uri);
            var cookie = cookies["dvld_rt"];
            if (cookie is not null && !string.IsNullOrWhiteSpace(cookie.Value))
            {
                return cookie.Value;
            }
        }

        return null;
    }

    private void InjectRefreshTokenCookie(string token)
    {
        var domain = BaseApiUri.Host;

        foreach (var path in new[] { "/api/Auth", "/api/auth", "/" })
        {
            try
            {
                var cookie = new Cookie("dvld_rt", token, path, domain)
                {
                    HttpOnly = true
                };
                _cookieContainer.Add(cookie);
            }
            catch
            {
            }
        }
    }

    private void ClearCookies()
    {
        var domain = BaseApiUri.Host;

        foreach (var path in new[] { "/api/Auth", "/api/auth", "/" })
        {
            try
            {
                var expiredCookie = new Cookie("dvld_rt", string.Empty, path, domain)
                {
                    Expired = true,
                    Expires = DateTime.UtcNow.AddDays(-1)
                };
                _cookieContainer.Add(expiredCookie);
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopRenewalTimer();
        _refreshLock.Dispose();
    }
}
