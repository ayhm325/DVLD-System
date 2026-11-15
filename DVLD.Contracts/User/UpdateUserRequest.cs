namespace DVLD.Contracts.User;

/// <summary>
/// Role is optional for backward compatibility:
/// null means "keep the current role unchanged".
/// </summary>
public sealed record UpdateUserRequest(
    int PersonId,
    string UserName,
    bool IsActive,
    int? Role = null);
