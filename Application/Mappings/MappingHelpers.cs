namespace Application.Mappers;

internal static class MappingHelpers
{
    internal static string EncodeRowVersion(byte[]? rowVersion) =>
        rowVersion is { Length: > 0 }
            ? Convert.ToBase64String(rowVersion)
            : string.Empty;

    internal static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
