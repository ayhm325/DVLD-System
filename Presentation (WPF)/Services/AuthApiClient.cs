using DVLD.Contracts.Auth;
using DVLD.Contracts.User;
using Presentation.Services.Results;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Presentation.Services;

public sealed class AuthApiClient : IAuthApiClient
{
    private const string CsrfHeaderName = "X-CSRF-Protection";
    private const string CsrfHeaderValue = "1";

    private readonly HttpClient _httpClient;
    private readonly ICurrentUserSession _currentUser;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AuthApiClient(HttpClient httpClient, ICurrentUserSession currentUser)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/login", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);

                return result is null
                    ? ApiResult<LoginResponse>.Failure(
                        "The API returned an empty response.", response.StatusCode)
                    : ApiResult<LoginResponse>.Success(result, response.StatusCode);
            }

            return ApiResult<LoginResponse>.Failure(
                await ExtractErrorMessageAsync(response), response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult<LoginResponse>.Failure(
                "Unable to connect to the DVLD API.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult<LoginResponse>.Failure(
                "The request to the DVLD API timed out.");
        }
    }

    public async Task<ApiResult<LoginResponse>> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post, "api/auth/refresh");

            httpRequest.Headers.Add(CsrfHeaderName, CsrfHeaderValue);

            using var response = await _httpClient.SendAsync(
                httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);

                return result is null
                    ? ApiResult<LoginResponse>.Failure(
                        "The API returned an empty response.", response.StatusCode)
                    : ApiResult<LoginResponse>.Success(result, response.StatusCode);
            }

            return ApiResult<LoginResponse>.Failure(
                await ExtractErrorMessageAsync(response), response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult<LoginResponse>.Failure(
                "Unable to connect to the DVLD API.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult<LoginResponse>.Failure(
                "The request to the DVLD API timed out.");
        }
    }

    public async Task<ApiResult> LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post, "api/auth/logout");

            httpRequest.Headers.Add(CsrfHeaderName, CsrfHeaderValue);
            AddAuthorizationHeader(httpRequest);

            using var response = await _httpClient.SendAsync(
                httpRequest, cancellationToken);

            return response.IsSuccessStatusCode
                ? ApiResult.Success(response.StatusCode)
                : ApiResult.Failure(
                    await ExtractErrorMessageAsync(response), response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure(
                "Unable to connect to the DVLD API.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult.Failure(
                "The request to the DVLD API timed out.");
        }
    }

    public async Task<ApiResult> LogoutAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post, "api/auth/logout-all");

            httpRequest.Headers.Add(CsrfHeaderName, CsrfHeaderValue);
            AddAuthorizationHeader(httpRequest);

            using var response = await _httpClient.SendAsync(
                httpRequest, cancellationToken);

            return response.IsSuccessStatusCode
                ? ApiResult.Success(response.StatusCode)
                : ApiResult.Failure(
                    await ExtractErrorMessageAsync(response), response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure(
                "Unable to connect to the DVLD API.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult.Failure(
                "The request to the DVLD API timed out.");
        }
    }

    public async Task<ApiResult<LoginResponse>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post, "api/auth/change-password")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            httpRequest.Headers.Add(CsrfHeaderName, CsrfHeaderValue);
            AddAuthorizationHeader(httpRequest);

            using var response = await _httpClient.SendAsync(
                httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);

                return result is null
                    ? ApiResult<LoginResponse>.Failure(
                        "The API returned an empty response.", response.StatusCode)
                    : ApiResult<LoginResponse>.Success(result, response.StatusCode);
            }

            return ApiResult<LoginResponse>.Failure(
                await ExtractErrorMessageAsync(response), response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult<LoginResponse>.Failure(
                "Unable to connect to the DVLD API.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult<LoginResponse>.Failure(
                "The request to the DVLD API timed out.");
        }
    }

    public async Task<ApiResult<UserProfileResponse>> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get, "api/auth/profile");

            AddAuthorizationHeader(httpRequest);

            using var response = await _httpClient.SendAsync(
                httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync<UserProfileResponse>(
                        JsonOptions, cancellationToken);

                return result is null
                    ? ApiResult<UserProfileResponse>.Failure(
                        "The API returned an empty profile response.", response.StatusCode)
                    : ApiResult<UserProfileResponse>.Success(result, response.StatusCode);
            }

            return ApiResult<UserProfileResponse>.Failure(
                await ExtractErrorMessageAsync(response), response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult<UserProfileResponse>.Failure(
                "Unable to connect to the DVLD API.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult<UserProfileResponse>.Failure(
                "The request to the DVLD API timed out.");
        }
    }

    private void AddAuthorizationHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.AccessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", _currentUser.AccessToken);
        }
    }

    private static async Task<string> ExtractErrorMessageAsync(
        HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(content))
        {
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized =>
                    "Authentication failed or session expired.",
                HttpStatusCode.Forbidden =>
                    "You are not authorized to perform this operation.",
                HttpStatusCode.NotFound =>
                    "The requested resource was not found.",
                HttpStatusCode.Conflict =>
                    "The operation conflicts with the current data.",
                _ => "The API request failed."
            };
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            foreach (var property in new[] { "error", "message", "detail" })
            {
                if (root.TryGetProperty(property, out var value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? "The API request failed.";
                }
            }
        }
        catch (JsonException)
        {
        }

        return content;
    }
}
