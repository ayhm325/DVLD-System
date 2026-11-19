using API.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Application.Common.Results;
using Application.DTOs.AuthDTO;
using Application.DTOs.UserDTO;
using Domain.Enums;
using DVLD.Contracts.User;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace API.IntegrationTests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Me_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new ApiWebApplicationFactory();
        var response = await factory.CreateClient().GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new ApiWebApplicationFactory();
        var response = await factory.CreateClient().GetAsync("/api/Auth/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WhenAuthenticated_ReturnsCurrentUserProfile()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        factory.UserServiceMock
            .Setup(x => x.GetCurrentProfileAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserProfileDto>.Success(new()
            {
                UserId = 10,
                PersonId = 20,
                UserName = "testuser",
                IsActive = true,
                FullName = "Test User",
                NationalNo = "N123",
                FirstName = "Test",
                SecondName = "User",
                LastName = "User",
                DateOfBirth = new DateTime(1993, 1, 1),
                Gender = 1,
                Address = "Test Address",
                Phone = "0790000000",
                Email = "test@example.com",
                NationalityCountryID = 1,
                CountryName = "Jordan",
                RowVersion = "AQIDBAUGBwg="
            }));

        var response = await Authenticated(factory, 10)
            .GetAsync("/api/Auth/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<UserProfileResponse>();

        Assert.NotNull(body);
        Assert.Equal(10, body.UserId);
        Assert.Equal(20, body.PersonId);
        Assert.Equal("testuser", body.UserName);
        Assert.True(body.IsActive);
        Assert.Equal("Test User", body.FullName);
        Assert.Equal("N123", body.NationalNo);
        Assert.Equal("Test Address", body.Address);
        Assert.Equal("0790000000", body.Phone);
        Assert.Equal("test@example.com", body.Email);
        Assert.Equal("Jordan", body.CountryName);
        Assert.Equal("AQIDBAUGBwg=", body.RowVersion);

        factory.UserServiceMock.Verify(
            x => x.GetCurrentProfileAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Profile_WhenUserNotFound_ReturnsNotFound()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        factory.UserServiceMock
            .Setup(x => x.GetCurrentProfileAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<UserProfileDto>.FromNotFound("User was not found."));

        var response = await Authenticated(factory, 10)
            .GetAsync("/api/Auth/profile");

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Resource not found",
            "User was not found.");
    }

    [Fact]
    public async Task Me_WhenAuthenticated_ReturnsOkWithUserInformation()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        var response = await Authenticated(factory, 10)
            .GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.NotNull(body);
        Assert.Equal(10, body.UserId);
        Assert.Equal("testuser", body.Username);
        Assert.Equal("Test User", body.FullName);
        Assert.Equal("Staff", body.Role);
    }

    [Fact]
    public async Task Login_WhenAuthServiceReturnsValidationFailure_ReturnsValidationProblemDetails()
    {
        await using var factory = new ApiWebApplicationFactory();
        const string error = "Username is required.\nPassword is required.";

        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LoginResponseDto>.FromValidationFailure(error));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/Auth/login",
            new { UserName = "", Password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var body = document.RootElement;

        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("Validation error", body.GetProperty("title").GetString());
        Assert.Equal(error, body.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("instance").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));

        var errors = body.GetProperty("errors").GetProperty("request");
        var messages = errors.EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Equal(2, messages.Count);
        Assert.Contains("Username is required.", messages);
        Assert.Contains("Password is required.", messages);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorizedProblemDetails()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LoginResponseDto>.FromUnauthorized("Invalid username or password."));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/Auth/login",
            new { UserName = "wronguser", Password = "wrongpassword" });

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            "Invalid username or password.");
    }

    [Fact]
    public async Task Login_WhenAuthServiceSucceeds_ReturnsOkWithCompleteResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(new()
            {
                AccessToken = "test-access-token",
                ExpiresAtUtc = expiresAt,
                UserId = 10,
                UserName = "testuser",
                PersonId = 20,
                FullName = "Test User",
                Role = UserRole.Staff
            }));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/Auth/login",
            new { UserName = "testuser", Password = "Password123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<DVLD.Contracts.Auth.LoginResponse>();

        Assert.NotNull(body);
        Assert.Equal("test-access-token", body.AccessToken);
        Assert.Equal(expiresAt, body.ExpiresAtUtc);
        Assert.Equal(10, body.UserId);
        Assert.Equal("testuser", body.UserName);
        Assert.Equal(20, body.PersonId);
        Assert.Equal("Test User", body.FullName);
        Assert.Equal("Staff", body.Role);

        factory.AuthServiceMock.Verify(
            x => x.LoginAsync(It.Is<LoginRequestDto>(d =>
                d.UserName == "testuser" &&
                d.Password == "Password123"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_WhenRateLimitIsExceeded_ReturnsTooManyRequests()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.FromUnauthorized("Invalid username or password."));

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/Auth/login",
                new
                {
                    UserName = "testuser",
                    Password = "WrongPassword123"
                });

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        var sixthResponse = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new
            {
                UserName = "testuser",
                Password = "WrongPassword123"
            });

        await AssertTooManyRequestsProblemDetailsAsync(
            sixthResponse,
            "/api/Auth/login",
            "WrongPassword123",
            "Invalid username or password");

        var seventhResponse = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new
            {
                UserName = "testuser",
                Password = "WrongPassword123"
            });

        await AssertTooManyRequestsProblemDetailsAsync(
            seventhResponse,
            "/api/Auth/login",
            "WrongPassword123",
            "Invalid username or password");

        factory.AuthServiceMock.Verify(
            x => x.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()),
            Times.Exactly(5));

        Assert.Equal(0, factory.DatabaseConnectionRecorder.ConnectionAttempts);
    }

    [Fact]
    public async Task LoginRateLimit_DoesNotAffectAuthenticatedEndpoints()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        var client = Authenticated(factory, 10);

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            await client.PostAsJsonAsync(
                "/api/Auth/login",
                new
                {
                    UserName = "testuser",
                    Password = "WrongPassword123"
                });
        }

        var response = await client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new ApiWebApplicationFactory();

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        factory.UserServiceMock.Verify(
            x => x.ChangePasswordAsync(
                It.IsAny<int>(),
                It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WhenServiceSucceeds_ReturnsFreshSession()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        factory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(
                10,
                It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        factory.AuthServiceMock
            .Setup(x => x.StartSessionAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(new LoginResponseDto
            {
                AccessToken = "fresh-access-token",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
                UserId = 10,
                UserName = "testuser",
                PersonId = 20,
                FullName = "Test User",
                Role = UserRole.Staff,
                RefreshToken = "fresh-refresh-token",
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(14)
            }));

        var response = await Authenticated(factory, 10).PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<DVLD.Contracts.Auth.LoginResponse>();

        Assert.NotNull(body);
        Assert.Equal("fresh-access-token", body.AccessToken);

        factory.UserServiceMock.Verify(
            x => x.ChangePasswordAsync(
                10,
                It.Is<ChangePasswordDto>(d =>
                    d.CurrentPassword == "OldPassword123" &&
                    d.NewPassword == "NewPassword123"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(
        "Current password is required.",
        400,
        "Validation error")]
    [InlineData(
        "Current password is incorrect.",
        400,
        "Validation error")]
    [InlineData(
        "User was not found.",
        404,
        "Resource not found")]
    [InlineData(
        "Password change conflict.",
        409,
        "Conflict")]
    [InlineData(
        "You are not allowed to change this password.",
        403,
        "Forbidden")]
    public async Task ChangePassword_WhenServiceReturnsFailure_ReturnsExpectedProblemDetails(
        string error,
        int status,
        string title)
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        var result = status switch
        {
            400 => Result.ValidationFailure(error),
            404 => Result.NotFound(error),
            409 => Result.Conflict(error),
            403 => Result.Forbidden(error),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        factory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(
                10,
                It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var response = await Authenticated(factory, 10).PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123"
            });

        await AssertProblemDetailsAsync(
            response,
            (HttpStatusCode)status,
            title,
            error);
    }

    [Fact]
    public async Task ChangePassword_WhenServiceReturnsFailure_ReturnsInternalServerError()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        factory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(
                10,
                It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Failure("Unexpected password change failure."));

        var response = await Authenticated(factory, 10).PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123"
            });

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.InternalServerError,
            "An unexpected error occurred.",
            "The server could not complete the request.");
    }

    [Fact]
    public async Task ChangePassword_WhenStartSessionFails_ReturnsSessionEstablishmentFailureAndClearsCookie()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        factory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(
                10,
                It.IsAny<ChangePasswordDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        factory.AuthServiceMock
            .Setup(x => x.StartSessionAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.FromFailure("Internal session database error."));

        var response = await Authenticated(factory, 10).PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123"
            });

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Session establishment failed",
            "Your password was changed successfully and all existing sessions were revoked, but establishing a new session failed. Please log in with your new password.");

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Internal session database error", content);
        Assert.DoesNotContain("AccessToken", content);
        Assert.DoesNotContain("RefreshToken", content);

        AssertRefreshCookieDeleted(response);
    }

    [Fact]
    public async Task ChangePassword_WhenStartSessionThrows_ReturnsSessionEstablishmentFailureAndClearsCookie()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);

        factory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(
                10,
                It.IsAny<ChangePasswordDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        factory.AuthServiceMock
            .Setup(x => x.StartSessionAsync(10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fatal database down exception."));

        var response = await Authenticated(factory, 10).PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123"
            });

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Session establishment failed",
            "Your password was changed successfully and all existing sessions were revoked, but establishing a new session failed. Please log in with your new password.");

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Fatal database down exception", content);

        AssertRefreshCookieDeleted(response);
    }

    [Fact]
    public async Task JwtBearer_WhenTokenIsValidAndUserIsActive_AllowsAccessViaUserService()
    {
        await using var factory = new ApiWebApplicationFactory();

        factory.AuthServiceMock
            .Setup(x => x.IsPrincipalCurrentAsync(
                10, "stamp-jwt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        SetupCurrentUser(factory, 10, "jwtuser", "Jwt User", UserRole.Staff);

        var expectedSessionId = Guid.NewGuid();
        factory.AuthServiceMock
            .Setup(x => x.IsPrincipalCurrentAsync(
                10, "stamp-jwt", expectedSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await using var jwtFactory = factory.WithWebHostBuilder(builder =>
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
            });
        });

        using var scope = jwtFactory.Services.CreateScope();
        var jwtTokenService =
            scope.ServiceProvider.GetRequiredService<Application.Interfaces.IJwtTokenService>();

        var tokenResult = jwtTokenService.GenerateToken(new JwtTokenRequest
        {
            UserId = 10,
            PersonId = 20,
            UserName = "jwtuser",
            FullName = "Jwt User",
            Role = UserRole.Staff,
            SecurityStamp = "stamp-jwt",
            SessionId = expectedSessionId
        });

        var client = jwtFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        var response = await client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.AuthServiceMock.Verify(
            x => x.IsPrincipalCurrentAsync(
                10, "stamp-jwt", expectedSessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JwtBearer_WhenUserIsInactiveOrMissing_ReturnsUnauthorizedViaUserService()
    {
        await using var factory = new ApiWebApplicationFactory();

        var expectedSessionId = Guid.NewGuid();
        factory.AuthServiceMock
            .Setup(x => x.IsPrincipalCurrentAsync(
                10, It.IsAny<string>(), expectedSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await using var jwtFactory = factory.WithWebHostBuilder(builder =>
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
            });
        });

        using var scope = jwtFactory.Services.CreateScope();
        var jwtTokenService =
            scope.ServiceProvider.GetRequiredService<Application.Interfaces.IJwtTokenService>();

        var tokenResult = jwtTokenService.GenerateToken(new JwtTokenRequest
        {
            UserId = 10,
            PersonId = 20,
            UserName = "inactiveuser",
            FullName = "Inactive User",
            Role = UserRole.Staff,
            SecurityStamp = "stamp-inactive",
            SessionId = expectedSessionId
        });

        var client = jwtFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        var response = await client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        factory.AuthServiceMock.Verify(
            x => x.IsPrincipalCurrentAsync(
                10, It.IsAny<string>(), expectedSessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void SetupCurrentUser(
        ApiWebApplicationFactory factory,
        int userId,
        string username = "testuser",
        string fullName = "Test User",
        UserRole role = UserRole.Staff)
    {
        factory.CurrentUserServiceMock
            .SetupGet(x => x.UserId)
            .Returns(userId);

        factory.CurrentUserServiceMock
            .SetupGet(x => x.Username)
            .Returns(username);

        factory.CurrentUserServiceMock
            .SetupGet(x => x.FullName)
            .Returns(fullName);

        factory.CurrentUserServiceMock
            .SetupGet(x => x.Role)
            .Returns(role);
    }

    private static HttpClient Authenticated(
        ApiWebApplicationFactory factory,
        int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-User-Id",
            userId.ToString());

        return client;
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedDetail)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var body = document.RootElement;

        Assert.Equal(
            (int)expectedStatus,
            body.GetProperty("status").GetInt32());

        Assert.Equal(
            expectedTitle,
            body.GetProperty("title").GetString());

        Assert.Equal(
            expectedDetail,
            body.GetProperty("detail").GetString());

        Assert.False(
            string.IsNullOrWhiteSpace(
                body.GetProperty("instance").GetString()));

        Assert.True(
            body.TryGetProperty("traceId", out var traceId));

        Assert.False(
            string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    private sealed class MeResponse
    {
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
    }

    [Fact]
    public async Task Login_WhenAuthServiceReturnsNullResult_Returns500InternalServerErrorWithoutThrowing()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<LoginResponseDto>)null! );

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/Auth/login",
            new { UserName = "testuser", Password = "Password123" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Login_WhenRequestIsNull_ReturnsBadRequest()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/Auth/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WhenLockAcquisitionFails_ReturnsConflictAndDoesNotClearCookie()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.AuthServiceMock
            .Setup(x => x.LogoutAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict("Failed to acquire session lock during logout. Retry the request."));

        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout");
        request.Headers.Add("X-CSRF-Protection", "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            Assert.DoesNotContain(setCookies, c => c.StartsWith("dvld_rt=;"));
        }
    }

    [Fact]
    public async Task Logout_WhenSuccessful_ReturnsNoContentAndClearsCookie()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.AuthServiceMock
            .Setup(x => x.LogoutAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout");
        request.Headers.Add("X-CSRF-Protection", "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertRefreshCookieDeleted(response);
    }

    private static void AssertRefreshCookieDeleted(HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.TryGetValues("Set-Cookie", out var setCookies),
            "Expected Set-Cookie header to be present on the response.");

        var cookie = Assert.Single(setCookies, c => c.StartsWith("dvld_rt="));
        Assert.Contains("dvld_rt=;", cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970 00:00:00 GMT", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/Auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_WhenRateLimitIsExceeded_ReturnsTooManyRequestsAndDoesNotInvokeAuthService()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        const string fakeRefreshToken = "invalid-test-refresh-token";
        factory.AuthServiceMock
            .Setup(x => x.RefreshAsync(fakeRefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.FromUnauthorized("Invalid or expired refresh token."));

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            using var request = CreateRefreshRequest(fakeRefreshToken);
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var request31 = CreateRefreshRequest(fakeRefreshToken);
        var response31 = await client.SendAsync(request31);

        await AssertTooManyRequestsProblemDetailsAsync(
            response31,
            "/api/Auth/refresh",
            fakeRefreshToken,
            "Invalid or expired refresh token");

        using var request32 = CreateRefreshRequest(fakeRefreshToken);
        var response32 = await client.SendAsync(request32);

        await AssertTooManyRequestsProblemDetailsAsync(
            response32,
            "/api/Auth/refresh",
            fakeRefreshToken,
            "Invalid or expired refresh token");

        factory.AuthServiceMock.Verify(
            x => x.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(30));

        Assert.Equal(0, factory.DatabaseConnectionRecorder.ConnectionAttempts);
    }

    [Fact]
    public async Task ChangePassword_WhenRateLimitIsExceeded_ReturnsTooManyRequestsAndDoesNotInvokePasswordChange()
    {
        await using var factory = new ApiWebApplicationFactory();
        SetupCurrentUser(factory, 10);
        using var client = Authenticated(factory, 10);

        const string fakeCurrentPassword = "FakeCurrentPassword1!";
        const string fakeNewPassword = "FakeNewPassword2!";

        factory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(10, It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.ValidationFailure("Current password is incorrect."));

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/Auth/change-password",
                new
                {
                    CurrentPassword = fakeCurrentPassword,
                    NewPassword = fakeNewPassword
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var sixthResponse = await client.PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = fakeCurrentPassword,
                NewPassword = fakeNewPassword
            });

        await AssertTooManyRequestsProblemDetailsAsync(
            sixthResponse,
            "/api/Auth/change-password",
            fakeCurrentPassword,
            fakeNewPassword,
            "Current password is incorrect");

        var seventhResponse = await client.PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = fakeCurrentPassword,
                NewPassword = fakeNewPassword
            });

        await AssertTooManyRequestsProblemDetailsAsync(
            seventhResponse,
            "/api/Auth/change-password",
            fakeCurrentPassword,
            fakeNewPassword);

        factory.UserServiceMock.Verify(
            x => x.ChangePasswordAsync(
                It.IsAny<int>(),
                It.IsAny<ChangePasswordDto>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(5));

        factory.AuthServiceMock.Verify(
            x => x.StartSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.Equal(0, factory.DatabaseConnectionRecorder.ConnectionAttempts);
    }

    [Fact]
    public async Task ChangePassword_WhenRateLimitExceededWithJwtBearer_RejectsBeforeTokenValidationAndBusinessLogic()
    {
        await using var baseFactory = new ApiWebApplicationFactory();
        SetupCurrentUser(baseFactory, 10, "jwtuser", "Jwt User", UserRole.Staff);

        var expectedSessionId = Guid.NewGuid();
        baseFactory.AuthServiceMock
            .Setup(x => x.IsPrincipalCurrentAsync(
                10, "stamp-jwt", expectedSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        baseFactory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(10, It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.ValidationFailure("Current password is incorrect."));

        await using var jwtFactory = baseFactory.WithWebHostBuilder(builder =>
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
            });
        });

        using var scope = jwtFactory.Services.CreateScope();
        var jwtTokenService =
            scope.ServiceProvider.GetRequiredService<Application.Interfaces.IJwtTokenService>();

        var tokenResult = jwtTokenService.GenerateToken(new JwtTokenRequest
        {
            UserId = 10,
            PersonId = 20,
            UserName = "jwtuser",
            FullName = "Jwt User",
            Role = UserRole.Staff,
            SecurityStamp = "stamp-jwt",
            SessionId = expectedSessionId
        });

        using var client = jwtFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/Auth/change-password",
                new
                {
                    CurrentPassword = "FakeCurrentPassword1!",
                    NewPassword = "FakeNewPassword2!"
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var sixthResponse = await client.PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "FakeCurrentPassword1!",
                NewPassword = "FakeNewPassword2!"
            });

        await AssertTooManyRequestsProblemDetailsAsync(
            sixthResponse,
            "/api/Auth/change-password",
            "FakeCurrentPassword1!",
            "FakeNewPassword2!");

        // UseRateLimiter runs before UseAuthentication, so the 6th request is throttled
        // without triggering per-request database/session validation or password-change logic.
        baseFactory.AuthServiceMock.Verify(
            x => x.IsPrincipalCurrentAsync(10, "stamp-jwt", expectedSessionId, It.IsAny<CancellationToken>()),
            Times.Exactly(5));

        baseFactory.UserServiceMock.Verify(
            x => x.ChangePasswordAsync(10, It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()),
            Times.Exactly(5));

        Assert.Equal(0, baseFactory.DatabaseConnectionRecorder.ConnectionAttempts);
    }

    [Fact]
    public async Task ChangePassword_WhenUnauthenticatedRequestsExceedLimit_Returns401WithinLimitAnd429AboveLimit()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/Auth/change-password",
                new
                {
                    CurrentPassword = "FakeCurrentPassword1!",
                    NewPassword = "FakeNewPassword2!"
                });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var sixthResponse = await client.PostAsJsonAsync(
            "/api/Auth/change-password",
            new
            {
                CurrentPassword = "FakeCurrentPassword1!",
                NewPassword = "FakeNewPassword2!"
            });

        await AssertTooManyRequestsProblemDetailsAsync(
            sixthResponse,
            "/api/Auth/change-password");

        factory.UserServiceMock.Verify(
            x => x.ChangePasswordAsync(It.IsAny<int>(), It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.Equal(0, factory.DatabaseConnectionRecorder.ConnectionAttempts);
    }

    [Fact]
    public async Task RateLimitPolicies_MaintainIndependentQuotasAcrossAuthEndpoints()
    {
        // 1. Saturating LoginRateLimit leaves Refresh and ChangePassword unaffected.
        await using (var loginSaturatedFactory = new ApiWebApplicationFactory())
        {
            SetupAuthPolicyMocks(loginSaturatedFactory);
            using var client = Authenticated(loginSaturatedFactory, 10);

            for (var i = 0; i < 5; i++)
            {
                var r = await client.PostAsJsonAsync("/api/Auth/login", new { UserName = "u", Password = "p" });
                Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            }

            var blockedLogin = await client.PostAsJsonAsync("/api/Auth/login", new { UserName = "u", Password = "p" });
            Assert.Equal(HttpStatusCode.TooManyRequests, blockedLogin.StatusCode);

            using var refreshReq = CreateRefreshRequest("invalid-test-refresh");
            var refreshAfterLoginLimit = await client.SendAsync(refreshReq);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLoginLimit.StatusCode);

            var changePwdAfterLoginLimit = await client.PostAsJsonAsync(
                "/api/Auth/change-password",
                new { CurrentPassword = "FakeCurrentPassword1!", NewPassword = "FakeNewPassword2!" });
            Assert.Equal(HttpStatusCode.BadRequest, changePwdAfterLoginLimit.StatusCode);
            Assert.Equal(0, loginSaturatedFactory.DatabaseConnectionRecorder.ConnectionAttempts);
        }

        // 2. Saturating RefreshRateLimit leaves ChangePassword and Login unaffected.
        await using (var refreshSaturatedFactory = new ApiWebApplicationFactory())
        {
            SetupAuthPolicyMocks(refreshSaturatedFactory);
            using var client = Authenticated(refreshSaturatedFactory, 10);

            for (var i = 0; i < 30; i++)
            {
                using var req = CreateRefreshRequest("invalid-test-refresh");
                var r = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            }

            using var blockedRefreshReq = CreateRefreshRequest("invalid-test-refresh");
            var blockedRefresh = await client.SendAsync(blockedRefreshReq);
            Assert.Equal(HttpStatusCode.TooManyRequests, blockedRefresh.StatusCode);

            var changePwdAfterRefreshLimit = await client.PostAsJsonAsync(
                "/api/Auth/change-password",
                new { CurrentPassword = "FakeCurrentPassword1!", NewPassword = "FakeNewPassword2!" });
            Assert.Equal(HttpStatusCode.BadRequest, changePwdAfterRefreshLimit.StatusCode);

            var loginAfterRefreshLimit = await client.PostAsJsonAsync("/api/Auth/login", new { UserName = "u", Password = "p" });
            Assert.Equal(HttpStatusCode.Unauthorized, loginAfterRefreshLimit.StatusCode);
            Assert.Equal(0, refreshSaturatedFactory.DatabaseConnectionRecorder.ConnectionAttempts);
        }

        // 3. Saturating ChangePasswordRateLimit leaves Login and Refresh unaffected.
        await using (var changePwdSaturatedFactory = new ApiWebApplicationFactory())
        {
            SetupAuthPolicyMocks(changePwdSaturatedFactory);
            using var client = Authenticated(changePwdSaturatedFactory, 10);

            for (var i = 0; i < 5; i++)
            {
                var r = await client.PostAsJsonAsync(
                    "/api/Auth/change-password",
                    new { CurrentPassword = "FakeCurrentPassword1!", NewPassword = "FakeNewPassword2!" });
                Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            }

            var blockedChangePwd = await client.PostAsJsonAsync(
                "/api/Auth/change-password",
                new { CurrentPassword = "FakeCurrentPassword1!", NewPassword = "FakeNewPassword2!" });
            Assert.Equal(HttpStatusCode.TooManyRequests, blockedChangePwd.StatusCode);

            var loginAfterChangePwdLimit = await client.PostAsJsonAsync("/api/Auth/login", new { UserName = "u", Password = "p" });
            Assert.Equal(HttpStatusCode.Unauthorized, loginAfterChangePwdLimit.StatusCode);

            using var refreshReq = CreateRefreshRequest("invalid-test-refresh");
            var refreshAfterChangePwdLimit = await client.SendAsync(refreshReq);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterChangePwdLimit.StatusCode);
            Assert.Equal(0, changePwdSaturatedFactory.DatabaseConnectionRecorder.ConnectionAttempts);
        }
    }

    [Fact]
    public async Task RateLimitPolicies_PartitionIndependentlyByRemoteIpAddress()
    {
        await using var baseFactory = new ApiWebApplicationFactory();
        SetupAuthPolicyMocks(baseFactory);

        await using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddTransient<IStartupFilter, RemoteIpStartupFilter>();
            });
        });

        using var client = factory.CreateClient();

        // LoginRateLimit: exhaust 203.0.113.10, verify 203.0.113.11 still has its own quota.
        for (var i = 0; i < 5; i++)
        {
            using var req = CreateLoginRequest("u", "p", "203.0.113.10");
            var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        using (var blocked = CreateLoginRequest("u", "p", "203.0.113.10"))
        {
            var resp = await client.SendAsync(blocked);
            Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        }

        using (var otherIp = CreateLoginRequest("u", "p", "203.0.113.11"))
        {
            var resp = await client.SendAsync(otherIp);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        // RefreshRateLimit: exhaust 203.0.113.20, verify 203.0.113.21 still has its own quota.
        for (var i = 0; i < 30; i++)
        {
            using var req = CreateRefreshRequest("invalid-test-refresh", "203.0.113.20");
            var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        using (var blocked = CreateRefreshRequest("invalid-test-refresh", "203.0.113.20"))
        {
            var resp = await client.SendAsync(blocked);
            Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        }

        using (var otherIp = CreateRefreshRequest("invalid-test-refresh", "203.0.113.21"))
        {
            var resp = await client.SendAsync(otherIp);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        // ChangePasswordRateLimit: exhaust 203.0.113.30, verify 203.0.113.31 still has its own quota.
        for (var i = 0; i < 5; i++)
        {
            using var req = CreateChangePasswordRequest(10, "FakeCurrentPassword1!", "FakeNewPassword2!", "203.0.113.30");
            var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        using (var blocked = CreateChangePasswordRequest(10, "FakeCurrentPassword1!", "FakeNewPassword2!", "203.0.113.30"))
        {
            var resp = await client.SendAsync(blocked);
            Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        }

        using (var otherIp = CreateChangePasswordRequest(10, "FakeCurrentPassword1!", "FakeNewPassword2!", "203.0.113.31"))
        {
            var resp = await client.SendAsync(otherIp);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        Assert.Equal(0, baseFactory.DatabaseConnectionRecorder.ConnectionAttempts);
    }

    private static void SetupAuthPolicyMocks(ApiWebApplicationFactory factory)
    {
        SetupCurrentUser(factory, 10);

        factory.AuthServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.FromUnauthorized("Invalid username or password."));

        factory.AuthServiceMock
            .Setup(x => x.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.FromUnauthorized("Invalid or expired refresh token."));

        factory.UserServiceMock
            .Setup(x => x.ChangePasswordAsync(10, It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.ValidationFailure("Current password is incorrect."));
    }

    private static HttpRequestMessage CreateLoginRequest(string userName, string password, string remoteIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/login")
        {
            Content = JsonContent.Create(new { UserName = userName, Password = password })
        };
        request.Headers.Add("X-Test-Remote-Ip", remoteIp);
        return request;
    }

    private static HttpRequestMessage CreateRefreshRequest(string refreshToken, string? remoteIp = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/refresh");
        request.Headers.Add("X-CSRF-Protection", "1");
        request.Headers.Add("Cookie", $"dvld_rt={refreshToken}");

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            request.Headers.Add("X-Test-Remote-Ip", remoteIp);
        }

        return request;
    }

    private static HttpRequestMessage CreateChangePasswordRequest(
        int userId,
        string currentPassword,
        string newPassword,
        string remoteIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/change-password")
        {
            Content = JsonContent.Create(new { CurrentPassword = currentPassword, NewPassword = newPassword })
        };
        request.Headers.Add("X-Test-User-Id", userId.ToString());
        request.Headers.Add("X-Test-Remote-Ip", remoteIp);
        return request;
    }

    private static async Task AssertTooManyRequestsProblemDetailsAsync(
        HttpResponseMessage response,
        string expectedInstance,
        params string[] forbiddenSubstrings)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.False(response.Headers.Contains("Retry-After"));

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", rawBody, StringComparison.Ordinal);

        foreach (var forbidden in forbiddenSubstrings)
        {
            Assert.DoesNotContain(forbidden, rawBody, StringComparison.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(rawBody);
        var body = document.RootElement;

        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.Equal("urn:dvld:error:too-many-requests", body.GetProperty("type").GetString());
        Assert.Equal("Too Many Requests", body.GetProperty("title").GetString());
        Assert.Equal(
            "Too many requests were received. Please retry later.",
            body.GetProperty("detail").GetString());
        Assert.Equal(expectedInstance, body.GetProperty("instance").GetString());
        Assert.True(body.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Test-Remote-Ip", out var header) &&
                        IPAddress.TryParse(header.FirstOrDefault(), out var remoteIp))
                    {
                        context.Connection.RemoteIpAddress = remoteIp;
                    }

                    await nextMiddleware();
                });

                next(app);
            };
    }
}
