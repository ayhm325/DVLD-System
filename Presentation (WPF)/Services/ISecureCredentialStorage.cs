namespace Presentation.Services;

/// <summary>
/// Provides secure local persistence for sensitive client credentials using OS-level protection.
/// </summary>
public interface ISecureCredentialStorage
{
    /// <summary>
    /// Encrypts and securely persists the refresh token.
    /// </summary>
    void SaveRefreshToken(string refreshToken);

    /// <summary>
    /// Loads and decrypts the persisted refresh token, or returns null if not found or invalid.
    /// </summary>
    string? LoadRefreshToken();

    /// <summary>
    /// Securely deletes any persisted credentials.
    /// </summary>
    void Clear();

    /// <summary>
    /// Determines whether an encrypted credential file exists on disk.
    /// </summary>
    bool HasStoredCredential();
}
