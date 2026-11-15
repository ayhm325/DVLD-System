using Presentation.Services;
using Xunit;

namespace Presentation.Tests;

public sealed class CurrentUserServiceTests
{
    private readonly CurrentUserService _service = new();

    [Fact]
    public void InitialState_IsNotLoggedIn()
    {
        Assert.False(_service.IsLoggedIn);
        Assert.Equal(0, _service.UserId);
        Assert.Equal(0, _service.PersonId);
        Assert.Empty(_service.Username);
        Assert.Empty(_service.FullName);
        Assert.Empty(_service.Role);
        Assert.Empty(_service.AccessToken);
        Assert.Null(_service.ExpiresAtUtc);
    }

    [Fact]
    public void SetSession_SetsAllPropertiesCorrectly()
    {
        var expires = DateTime.UtcNow.AddMinutes(15);

        _service.SetSession(
            userId: 42,
            personId: 10,
            username: "admin_user",
            fullName: "Admin Officer",
            role: "Admin",
            accessToken: "valid.jwt.token",
            expiresAtUtc: expires);

        Assert.True(_service.IsLoggedIn);
        Assert.Equal(42, _service.UserId);
        Assert.Equal(10, _service.PersonId);
        Assert.Equal("admin_user", _service.Username);
        Assert.Equal("Admin Officer", _service.FullName);
        Assert.Equal("Admin", _service.Role);
        Assert.Equal("valid.jwt.token", _service.AccessToken);
        Assert.Equal(expires, _service.ExpiresAtUtc);
    }

    [Fact]
    public void UpdateToken_UpdatesAccessTokenAndExpiry()
    {
        _service.SetSession(1, 2, "user", "name", "Staff", "initial.token");
        var newExpiry = DateTime.UtcNow.AddMinutes(20);

        _service.UpdateToken("rotated.token", newExpiry);

        Assert.Equal("rotated.token", _service.AccessToken);
        Assert.Equal(newExpiry, _service.ExpiresAtUtc);
        Assert.Equal(1, _service.UserId);
        Assert.Equal("user", _service.Username);
    }

    [Fact]
    public void Clear_ResetsAllProperties()
    {
        _service.SetSession(1, 2, "user", "name", "Staff", "token");
        Assert.True(_service.IsLoggedIn);

        _service.Clear();

        Assert.False(_service.IsLoggedIn);
        Assert.Equal(0, _service.UserId);
        Assert.Empty(_service.AccessToken);
        Assert.Null(_service.ExpiresAtUtc);
    }
}
