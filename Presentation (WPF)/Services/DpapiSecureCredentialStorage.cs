using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Presentation.Services;

/// <summary>
/// Implements secure credential persistence using Windows Data Protection API (DPAPI)
/// scoped to the current Windows user profile.
/// </summary>
public sealed class DpapiSecureCredentialStorage : ISecureCredentialStorage
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DVLD-Desktop-Session-v1");

    private readonly string _storageFilePath;

    public DpapiSecureCredentialStorage()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dvldDir = Path.Combine(localAppData, "DVLD");
        _storageFilePath = Path.Combine(dvldDir, "session.dat");
    }

    public DpapiSecureCredentialStorage(string customFilePath)
    {
        _storageFilePath = customFilePath ?? throw new ArgumentNullException(nameof(customFilePath));
    }

    public void SaveRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            Clear();
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var plainBytes = Encoding.UTF8.GetBytes(refreshToken);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            // Write to a temporary file first then replace/move for atomic write
            var tempFile = _storageFilePath + ".tmp";
            File.WriteAllBytes(tempFile, encryptedBytes);
            File.Move(tempFile, _storageFilePath, overwrite: true);
        }
        catch (Exception)
        {
            // Do not crash the application if local filesystem persistence fails
        }
    }

    public string? LoadRefreshToken()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                return null;
            }

            var encryptedBytes = File.ReadAllBytes(_storageFilePath);
            if (encryptedBytes.Length == 0)
            {
                Clear();
                return null;
            }

            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (CryptographicException)
        {
            // The file cannot be decrypted by the current user (e.g. copied from another machine or corrupted)
            Clear();
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_storageFilePath))
            {
                File.Delete(_storageFilePath);
            }

            var tempFile = _storageFilePath + ".tmp";
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
        catch (Exception)
        {
            // Ignored on cleanup
        }
    }

    public bool HasStoredCredential()
    {
        return File.Exists(_storageFilePath);
    }
}
