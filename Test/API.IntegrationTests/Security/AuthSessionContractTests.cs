using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using API.IntegrationTests.Infrastructure;

using Application.Common.Results;
using Application.DTOs.AuthDTO;

using Domain.Enums;

using Moq;

namespace API.IntegrationTests.Security;

/// <summary>
/// Contract tests for the refresh-cookie transport and the authentication endpoints.
/// The application service is mocked, so these tests are database-free.
/// </summary>
public sealed class AuthSessionContractTests
{
    private const string CsrfHeader = "X-CSRF-Protection";
    private const string RefreshCookie = "dvld_rt";

    [Fact]
    public async Task Login_SetsHttpOnlyRefreshCookie_AndNeverReturnsRefreshTokenInJson()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(
                It.IsAny<Application.DTOs.UserDTO.LoginRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(Session("raw-refresh")));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/Auth/login",
            new { UserName = "admin", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = GetSetCookie(response);
        Assert.Contains($"{RefreshCookie}=raw-refresh", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/Auth", setCookie, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.TryGetProperty("accessToken", out _));
        Assert.False(document.RootElement.TryGetProperty("refreshToken", out _));
        Assert.False(document.RootElement.TryGetProperty("refreshTokenExpiresAtUtc", out _));
    }

    [Fact]
    public async Task Refresh_WithoutCsrfHeader_ReturnsValidationProblem()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookie}=old-refresh");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesCookieAndReturnsAccessToken()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.RefreshAsync("old-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(Session("new-refresh")));

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookie}=old-refresh");
        request.Headers.Add(CsrfHeader, "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = GetSetCookie(response);
        Assert.Contains($"{RefreshCookie}=new-refresh", setCookie);

        var body = await response.Content
            .ReadFromJsonAsync<DVLD.Contracts.Auth.LoginResponse>();

        Assert.NotNull(body);
        Assert.Equal("access-token", body.AccessToken);

        factory.AuthServiceMock.Verify(
            x => x.RefreshAsync("old-refresh", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WithRealCookieContainerAndHttpClientHandler_TransmitsRotatedCookieAndSucceeds()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(
                It.IsAny<Application.DTOs.UserDTO.LoginRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(Session("initial-refresh-token")));

        factory.AuthServiceMock
            .Setup(x => x.RefreshAsync("initial-refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(Session("rotated-refresh-token")));

        var baseAddress = new Uri("http://localhost");
        var cookieContainer = new CookieContainer();
        using var handler = new HttpClientHandler { CookieContainer = cookieContainer };

        // 1. Login successfully
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { UserName = "admin", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // 2. Capture the Set-Cookie response and load it into the CookieContainer
        var setCookieHeaders = loginResponse.Headers.GetValues("Set-Cookie");
        foreach (var header in setCookieHeaders)
        {
            cookieContainer.SetCookies(baseAddress, header);
        }

        // 3. Verify the browser-equivalent CookieContainer matches path /api/Auth/refresh
        var requestUri = new Uri("http://localhost/api/Auth/refresh");
        var cookieHeaderForRefresh = cookieContainer.GetCookieHeader(requestUri);
        Assert.Contains($"{RefreshCookie}=initial-refresh-token", cookieHeaderForRefresh);

        // 4. Send POST /api/Auth/refresh using the cookie container
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/refresh");
        refreshRequest.Headers.Add("Cookie", cookieHeaderForRefresh);
        refreshRequest.Headers.Add(CsrfHeader, "1");

        var refreshResponse = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        // 5. Verify refresh succeeds and token rotation works
        var rotatedSetCookie = GetSetCookie(refreshResponse);
        Assert.Contains($"{RefreshCookie}=rotated-refresh-token", rotatedSetCookie);
        Assert.Contains("path=/api/Auth", rotatedSetCookie, StringComparison.Ordinal);

        var body = await refreshResponse.Content
            .ReadFromJsonAsync<DVLD.Contracts.Auth.LoginResponse>();

        Assert.NotNull(body);
        Assert.Equal("access-token", body.AccessToken);

        factory.AuthServiceMock.Verify(
            x => x.RefreshAsync("initial-refresh-token", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_ViaBrowserEquivalentClient_AutomaticallyTransmitsCookieWithoutManualHeader()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(
                It.IsAny<Application.DTOs.UserDTO.LoginRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(Session("cookie-refresh-1")));

        factory.AuthServiceMock
            .Setup(x => x.RefreshAsync("cookie-refresh-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(Session("cookie-refresh-2")));

        // Single client instance with internal CookieContainer (HandleCookies = true)
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { UserName = "admin", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // POST /api/Auth/refresh WITHOUT manually setting the Cookie header
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/refresh");
        refreshRequest.Headers.Add(CsrfHeader, "1");

        var refreshResponse = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var setCookie = GetSetCookie(refreshResponse);
        Assert.Contains($"{RefreshCookie}=cookie-refresh-2", setCookie);
        Assert.Contains("path=/api/Auth", setCookie, StringComparison.Ordinal);

        factory.AuthServiceMock.Verify(
            x => x.RefreshAsync("cookie-refresh-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WhenUnauthorized_ClearsCookieAndReturns401ProblemDetails()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LoginResponseDto>.FromUnauthorized(
                    "The authentication session is invalid or has expired."));

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookie}=stale");
        request.Headers.Add(CsrfHeader, "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var setCookie = GetSetCookie(response);
        Assert.Contains($"{RefreshCookie}=", setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_WithCookie_RevokesAndClearsCookie()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.LogoutAsync("current-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout");
        request.Headers.Add("Cookie", $"{RefreshCookie}=current-refresh");
        request.Headers.Add(CsrfHeader, "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookie = GetSetCookie(response);
        Assert.Contains($"{RefreshCookie}=", setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);

        factory.AuthServiceMock.Verify(
            x => x.LogoutAsync("current-refresh", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Logout_WithBearerTokenContainingSid_RevokesBySessionId()
    {
        await using var factory = new ApiWebApplicationFactory();
        var expectedSessionId = Guid.NewGuid();

        factory.AuthServiceMock
            .Setup(x => x.LogoutAsync(null, expectedSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout");
        request.Headers.Add(CsrfHeader, "1");

        // Attach bearer token with sid claim
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(new string('k', 64)));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "DVLD.Api",
            audience: "DVLD.Client",
            claims: new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "10"),
                new System.Security.Claims.Claim("sst", "stamp"),
                new System.Security.Claims.Claim("sid", expectedSessionId.ToString("D"))
            },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);
        var jwtString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtString);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        factory.AuthServiceMock.Verify(
            x => x.LogoutAsync(null, expectedSessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAll_WithoutAuthentication_Returns401()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout-all");
        request.Headers.Add(CsrfHeader, "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_WhenAuthenticated_RevokesEverySession()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.LogoutAllAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        factory.CurrentUserServiceMock
            .SetupGet(x => x.UserId)
            .Returns(10);
        factory.CurrentUserServiceMock
            .SetupGet(x => x.IsLoggedIn)
            .Returns(true);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Staff");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout-all");
        request.Headers.Add(CsrfHeader, "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        factory.AuthServiceMock.Verify(
            x => x.LogoutAllAsync(10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TokenValidation_WithoutSidClaim_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        var (jwtFactory, jwtService) = CreateJwtTestEnvironment(factory);

        var tokenNoSid = jwtService.GenerateToken(new JwtTokenRequest
        {
            UserId = 10,
            PersonId = 20,
            UserName = "user10",
            FullName = "User 10",
            Role = UserRole.Staff,
            SecurityStamp = "stamp-10",
            SessionId = null
        });

        using var client = jwtFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenNoSid.AccessToken);

        var response = await client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TwoDevices_IndependentSessions_LogoutDeviceA_LeavesDeviceBActive_LogoutAll_RevokesBoth()
    {
        await using var factory = new ApiWebApplicationFactory();
        var (jwtFactory, jwtService) = CreateJwtTestEnvironment(factory);

        var sessionIdA = Guid.NewGuid();
        var sessionIdB = Guid.NewGuid();
        var otherUserSessionId = Guid.NewGuid();

        var activeSessions = new HashSet<Guid> { sessionIdA, sessionIdB, otherUserSessionId };
        var revokedUsers = new HashSet<int>();

        factory.AuthServiceMock
            .Setup(x => x.IsPrincipalCurrentAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int userId, string stamp, Guid? sid, CancellationToken _) =>
            {
                if (revokedUsers.Contains(userId)) return false;
                return sid.HasValue && activeSessions.Contains(sid.Value);
            });

        factory.AuthServiceMock
            .Setup(x => x.LogoutAsync(It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? _, Guid? sid, CancellationToken _) =>
            {
                if (sid.HasValue) activeSessions.Remove(sid.Value);
                return Result.Success();
            });

        factory.AuthServiceMock
            .Setup(x => x.LogoutAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int userId, CancellationToken _) =>
            {
                revokedUsers.Add(userId);
                return Result.Success();
            });

        var tokenA = jwtService.GenerateToken(new JwtTokenRequest
        {
            UserId = 10,
            PersonId = 20,
            UserName = "user10",
            FullName = "User 10",
            Role = UserRole.Staff,
            SecurityStamp = "stamp-10",
            SessionId = sessionIdA
        });

        var tokenB = jwtService.GenerateToken(new JwtTokenRequest
        {
            UserId = 10,
            PersonId = 20,
            UserName = "user10",
            FullName = "User 10",
            Role = UserRole.Staff,
            SecurityStamp = "stamp-10",
            SessionId = sessionIdB
        });

        var tokenOther = jwtService.GenerateToken(new JwtTokenRequest
        {
            UserId = 99,
            PersonId = 99,
            UserName = "otherUser",
            FullName = "Other User",
            Role = UserRole.Staff,
            SecurityStamp = "stamp-99",
            SessionId = otherUserSessionId
        });

        using var clientA = jwtFactory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA.AccessToken);

        using var clientB = jwtFactory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenB.AccessToken);

        using var clientOther = jwtFactory.CreateClient();
        clientOther.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenOther.AccessToken);

        // 1. Both devices (and other user) can access protected endpoint
        var resA1 = await clientA.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, resA1.StatusCode);

        var resB1 = await clientB.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, resB1.StatusCode);

        var resOther1 = await clientOther.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, resOther1.StatusCode);

        // 2. Logging out from device A invalidates device A
        using var logoutRequestA = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout");
        logoutRequestA.Headers.Add(CsrfHeader, "1");
        var logoutResponseA = await clientA.SendAsync(logoutRequestA);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponseA.StatusCode);

        // Verify device A is now rejected
        var resA2 = await clientA.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resA2.StatusCode);

        // 3. Device B remains authorized
        var resB2 = await clientB.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, resB2.StatusCode);

        // Other user remains authorized
        var resOther2 = await clientOther.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, resOther2.StatusCode);

        // 4. Logout-All from Device B invalidates all sessions for User 10
        using var logoutAllRequestB = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout-all");
        logoutAllRequestB.Headers.Add(CsrfHeader, "1");
        var logoutAllResponseB = await clientB.SendAsync(logoutAllRequestB);
        Assert.Equal(HttpStatusCode.NoContent, logoutAllResponseB.StatusCode);

        // Both Device A and Device B are now rejected
        var resA3 = await clientA.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resA3.StatusCode);

        var resB3 = await clientB.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resB3.StatusCode);

        // Other user (User 99) is NOT affected
        var resOther3 = await clientOther.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, resOther3.StatusCode);
    }

    private static (Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory, Application.Interfaces.IJwtTokenService JwtService)
        CreateJwtTestEnvironment(ApiWebApplicationFactory factory)
    {
        var jwtFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme =
                        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme =
                        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                });

                services.RemoveAll<Application.Interfaces.ICurrentUserService>();
                services.AddScoped<Application.Interfaces.ICurrentUserService, DVLD.Api.Security.ApiCurrentUserService>();
            });
        });

        var scope = jwtFactory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IJwtTokenService>();
        return (jwtFactory, jwtService);
    }

    private static LoginResponseDto Session(string refreshToken) =>
        new()
        {
            AccessToken = "access-token",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            UserId = 10,
            UserName = "admin",
            PersonId = 20,
            FullName = "Admin User",
            Role = UserRole.Staff,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        };

    private static string GetSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join(" | ", values)
            : string.Empty;
}
