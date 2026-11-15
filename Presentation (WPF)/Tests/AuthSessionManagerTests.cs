using DVLD.Contracts.Auth;
using Moq;
using Presentation.Services;
using Presentation.Services.Results;
using System.Net;
using Xunit;

namespace Presentation.Tests;

public sealed class AuthSessionManagerTests
{
    private readonly Mock<IAuthApiClient> _mockAuthApi = new();
    private readonly CurrentUserService _currentUser = new();
    private readonly Mock<ISecureCredentialStorage> _mockStorage = new();
    private readonly CookieContainer _cookieContainer = new();

    private readonly AuthSessionManager _sessionManager;

    public AuthSessionManagerTests()
    {
        _sessionManager = new AuthSessionManager(
            _mockAuthApi.Object,
            _currentUser,
            _mockStorage.Object,
            _cookieContainer);
    }

    [Fact]
    public async Task LoginAsync_OnSuccess_AppliesSessionAndSavesCredentialWhenRememberMe()
    {
        var loginResponse = new LoginResponse
        {
            UserId = 10,
            PersonId = 20,
            UserName = "officer",
            FullName = "Police Officer",
            Role = "Staff",
            AccessToken = "jwt.access.token",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        _mockAuthApi
            .Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<LoginResponse>.Success(loginResponse, HttpStatusCode.OK));

        // Inject refresh token cookie as if returned from server
        _cookieContainer.Add(new Cookie("dvld_rt", "server.refresh.token", "/api/Auth", "localhost"));

        var result = await _sessionManager.LoginAsync("officer", "password123", rememberMe: true);

        Assert.True(result.IsSuccess);
        Assert.True(_sessionManager.IsLoggedIn);
        Assert.Equal(10, _currentUser.UserId);
        Assert.Equal("jwt.access.token", _currentUser.AccessToken);
        _mockStorage.Verify(s => s.SaveRefreshToken("server.refresh.token"), Times.Once);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenNoStoredCredential_ReturnsFalse()
    {
        _mockStorage.Setup(s => s.HasStoredCredential()).Returns(false);

        var restored = await _sessionManager.TryRestoreSessionAsync();

        Assert.False(restored);
        Assert.False(_sessionManager.IsLoggedIn);
        _mockAuthApi.Verify(a => a.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenStoredCredentialExists_RefreshesAndRestoresSession()
    {
        _mockStorage.Setup(s => s.HasStoredCredential()).Returns(true);
        _mockStorage.Setup(s => s.LoadRefreshToken()).Returns("saved.dpapi.token");

        var refreshResponse = new LoginResponse
        {
            UserId = 5,
            PersonId = 7,
            UserName = "restored_user",
            FullName = "Restored User",
            Role = "Admin",
            AccessToken = "restored.jwt.token",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        _mockAuthApi
            .Setup(a => a.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<LoginResponse>.Success(refreshResponse, HttpStatusCode.OK));

        var restored = await _sessionManager.TryRestoreSessionAsync();

        Assert.True(restored);
        Assert.True(_sessionManager.IsLoggedIn);
        Assert.Equal("restored_user", _currentUser.Username);
        Assert.Equal("restored.jwt.token", _currentUser.AccessToken);

        // Verify cookie was injected
        var cookies = _cookieContainer.GetCookies(new Uri("http://localhost:5260/api/Auth"));
        Assert.NotNull(cookies["dvld_rt"]);
    }

    [Fact]
    public async Task RefreshSessionAsync_On401Unauthorized_ClearsSessionAndFiresSessionExpired()
    {
        _currentUser.SetSession(1, 2, "user", "name", "Staff", "expired.token");
        Assert.True(_sessionManager.IsLoggedIn);

        _mockAuthApi
            .Setup(a => a.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<LoginResponse>.Failure("Session expired", HttpStatusCode.Unauthorized));

        var expiredFired = false;
        _sessionManager.SessionExpired += (s, e) => expiredFired = true;

        var success = await _sessionManager.RefreshSessionAsync();

        Assert.False(success);
        Assert.False(_sessionManager.IsLoggedIn);
        Assert.True(expiredFired);
        _mockStorage.Verify(s => s.Clear(), Times.Once);
    }

    [Fact]
    public async Task RefreshSessionAsync_OnTransientError_DoesNotClearSessionOrFireExpired()
    {
        _currentUser.SetSession(1, 2, "user", "name", "Staff", "valid.token");
        Assert.True(_sessionManager.IsLoggedIn);

        _mockAuthApi
            .Setup(a => a.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<LoginResponse>.Failure("Gateway timeout", HttpStatusCode.GatewayTimeout));

        var expiredFired = false;
        _sessionManager.SessionExpired += (s, e) => expiredFired = true;

        var success = await _sessionManager.RefreshSessionAsync();

        Assert.False(success);
        Assert.True(_sessionManager.IsLoggedIn);
        Assert.False(expiredFired);
        _mockStorage.Verify(s => s.Clear(), Times.Never);
    }

    [Fact]
    public async Task RefreshSessionAsync_On409Conflict_RetriesAndSucceeds()
    {
        var refreshResponse = new LoginResponse
        {
            UserId = 1,
            PersonId = 2,
            UserName = "user",
            FullName = "User Name",
            Role = "Staff",
            AccessToken = "retried.token",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        var callCount = 0;
        _mockAuthApi
            .Setup(a => a.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? ApiResult<LoginResponse>.Failure("Concurrent refresh race", HttpStatusCode.Conflict)
                    : ApiResult<LoginResponse>.Success(refreshResponse, HttpStatusCode.OK);
            });

        var success = await _sessionManager.RefreshSessionAsync();

        Assert.True(success);
        Assert.Equal(2, callCount);
        Assert.Equal("retried.token", _currentUser.AccessToken);
    }

    [Fact]
    public async Task RefreshSessionAsync_WhenCalledConcurrently_PerformsSingleFlight()
    {
        var refreshResponse = new LoginResponse
        {
            UserId = 1,
            PersonId = 2,
            UserName = "user",
            FullName = "User Name",
            Role = "Staff",
            AccessToken = "singleflight.token",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        var refreshInvocationCount = 0;
        _mockAuthApi
            .Setup(a => a.RefreshAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref refreshInvocationCount);
                await Task.Delay(100);
                return ApiResult<LoginResponse>.Success(refreshResponse, HttpStatusCode.OK);
            });

        // Launch 5 concurrent refresh tasks
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _sessionManager.RefreshSessionAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, Assert.True);
        Assert.Equal(1, refreshInvocationCount);
    }

    [Fact]
    public async Task LogoutAsync_CallsApiAndClearsLocalSession()
    {
        _currentUser.SetSession(1, 2, "user", "name", "Staff", "token");
        _mockAuthApi
            .Setup(a => a.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult.Success(HttpStatusCode.NoContent));

        var result = await _sessionManager.LogoutAsync();

        Assert.True(result.IsSuccess);
        Assert.False(_sessionManager.IsLoggedIn);
        _mockStorage.Verify(s => s.Clear(), Times.Once);
        _mockAuthApi.Verify(a => a.LogoutAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
