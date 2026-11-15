using Presentation.Services;
using System.IO;
using Xunit;

namespace Presentation.Tests;

public sealed class DpapiSecureCredentialStorageTests : IDisposable
{
    private readonly string _tempFile;
    private readonly DpapiSecureCredentialStorage _storage;

    public DpapiSecureCredentialStorageTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"dvld_test_session_{Guid.NewGuid():N}.dat");
        _storage = new DpapiSecureCredentialStorage(_tempFile);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);

            var tmp = _tempFile + ".tmp";
            if (File.Exists(tmp))
                File.Delete(tmp);
        }
        catch
        {
        }
    }

    [Fact]
    public void LoadRefreshToken_WhenNoFileExists_ReturnsNull()
    {
        var result = _storage.LoadRefreshToken();

        Assert.Null(result);
        Assert.False(_storage.HasStoredCredential());
    }

    [Fact]
    public void SaveRefreshToken_ThenLoad_ReturnsDecryptedToken()
    {
        const string expectedToken = "sample_secure_refresh_token_12345!@#$%^&*()";

        _storage.SaveRefreshToken(expectedToken);

        Assert.True(_storage.HasStoredCredential());
        var loaded = _storage.LoadRefreshToken();
        Assert.Equal(expectedToken, loaded);
    }

    [Fact]
    public void Clear_DeletesStoredFile()
    {
        _storage.SaveRefreshToken("token_to_clear");
        Assert.True(_storage.HasStoredCredential());

        _storage.Clear();

        Assert.False(_storage.HasStoredCredential());
        Assert.Null(_storage.LoadRefreshToken());
    }

    [Fact]
    public void SaveRefreshToken_WithNullOrEmpty_ClearsStorage()
    {
        _storage.SaveRefreshToken("initial_token");
        Assert.True(_storage.HasStoredCredential());

        _storage.SaveRefreshToken(string.Empty);

        Assert.False(_storage.HasStoredCredential());
        Assert.Null(_storage.LoadRefreshToken());
    }

    [Fact]
    public void LoadRefreshToken_WhenFileIsCorrupted_ReturnsNullAndClearsStorage()
    {
        File.WriteAllBytes(_tempFile, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var result = _storage.LoadRefreshToken();

        Assert.Null(result);
        Assert.False(_storage.HasStoredCredential());
    }
}
