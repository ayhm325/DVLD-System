using Moq;
using Moq.Protected;
using Presentation.Services;
using Presentation.Services.Api;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace Presentation.Tests;

public sealed class ApiClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHandler = new();
    private readonly CurrentUserService _currentUser = new();
    private readonly Mock<IAuthSessionManager> _mockSessionManager = new();
    private readonly ApiClient _apiClient;

    public ApiClientTests()
    {
        var httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5260/")
        };

        _apiClient = new ApiClient(httpClient, _currentUser, _mockSessionManager.Object);
    }

    private record TestDto(string Message);

    [Fact]
    public async Task GetAsync_When401AndRefreshSucceeds_RetriesRequestAndReturnsSuccess()
    {
        _currentUser.SetSession(1, 2, "user", "name", "Staff", "old.access.token");

        var invocationCount = 0;

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                invocationCount++;
                if (invocationCount == 1)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new TestDto("Success after refresh")),
                        System.Text.Encoding.UTF8,
                        "application/json")
                });
            });

        _mockSessionManager
            .Setup(s => s.RefreshSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _currentUser.UpdateToken("new.refreshed.access.token");
                return true;
            });

        var result = await _apiClient.GetAsync<TestDto>("api/test-endpoint");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Success after refresh", result.Value.Message);
        Assert.Equal(2, invocationCount);
        _mockSessionManager.Verify(s => s.RefreshSessionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_When401AndRefreshFails_ReturnsUnauthorizedFailure()
    {
        _currentUser.SetSession(1, 2, "user", "name", "Staff", "old.access.token");

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        _mockSessionManager
            .Setup(s => s.RefreshSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _apiClient.GetAsync<TestDto>("api/test-endpoint");

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        _mockSessionManager.Verify(s => s.RefreshSessionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
