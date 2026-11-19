using Serilog.Core;
using Serilog.Events;

namespace DVLD.Api.Configuration;

/// <summary>
/// Defense-in-depth Serilog enricher that redacts scalar properties whose names
/// indicate credentials, tokens, secrets, or sensitive personal information.
/// Primary protection remains avoiding sensitive properties at call sites.
/// </summary>
public sealed class SensitiveDataRedactingEnricher : ILogEventEnricher
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "CurrentPassword",
        "NewPassword",
        "PasswordHash",
        "Secret",
        "SecretKey",
        "JwtSecret",
        "AccessToken",
        "RefreshToken",
        "Token",
        "Authorization",
        "Cookie",
        "SetCookie",
        "ApiKey",
        "ConnectionString",
        "PrivateKey",
        "SecurityStamp",
        "NationalNo",
        "NationalNumber",
        "Phone",
        "PhoneNumber",
        "Address",
        "DateOfBirth",
        "Email",
        "FullName",
        "FirstName",
        "SecondName",
        "ThirdName",
        "LastName",
        "PersonName",
        "ApplicantName",
        "DriverName",
        "UserName",
        "Username",
        "ImagePath",
        "PhotoPath",
        "FilePath",
        "PhotoUrl",
        "StorageKey",
        "OldKey",
        "NewStorageKey"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        List<LogEventProperty>? updatedProperties = null;

        foreach (var property in logEvent.Properties)
        {
            if (IsSensitivePropertyName(property.Key))
            {
                updatedProperties ??= [];
                updatedProperties.Add(propertyFactory.CreateProperty(property.Key, RedactedValue));
            }
            else if (string.Equals(property.Key, "RequestPath", StringComparison.OrdinalIgnoreCase) &&
                     property.Value is ScalarValue { Value: string rawPath } &&
                     TrySanitizeRequestPath(rawPath, out var safePath))
            {
                updatedProperties ??= [];
                updatedProperties.Add(propertyFactory.CreateProperty("RequestPath", safePath));
            }
            else if (TryRedactNestedValue(property.Value, out var redactedNested))
            {
                updatedProperties ??= [];
                updatedProperties.Add(new LogEventProperty(property.Key, redactedNested));
            }
        }

        if (updatedProperties is null)
        {
            return;
        }

        foreach (var updated in updatedProperties)
        {
            logEvent.AddOrUpdateProperty(updated);
        }
    }

    private static bool TryRedactNestedValue(
        LogEventPropertyValue value,
        out LogEventPropertyValue redacted)
    {
        switch (value)
        {
            case StructureValue structure:
            {
                var changed = false;
                var newProps = new List<LogEventProperty>(structure.Properties.Count);
                foreach (var child in structure.Properties)
                {
                    if (IsSensitivePropertyName(child.Name))
                    {
                        newProps.Add(new LogEventProperty(child.Name, new ScalarValue(RedactedValue)));
                        changed = true;
                    }
                    else if (TryRedactNestedValue(child.Value, out var childRedacted))
                    {
                        newProps.Add(new LogEventProperty(child.Name, childRedacted));
                        changed = true;
                    }
                    else
                    {
                        newProps.Add(child);
                    }
                }

                if (changed)
                {
                    redacted = new StructureValue(newProps, structure.TypeTag);
                    return true;
                }

                redacted = value;
                return false;
            }

            case SequenceValue sequence:
            {
                var changed = false;
                var newElements = new List<LogEventPropertyValue>(sequence.Elements.Count);
                foreach (var element in sequence.Elements)
                {
                    if (TryRedactNestedValue(element, out var elemRedacted))
                    {
                        newElements.Add(elemRedacted);
                        changed = true;
                    }
                    else
                    {
                        newElements.Add(element);
                    }
                }

                if (changed)
                {
                    redacted = new SequenceValue(newElements);
                    return true;
                }

                redacted = value;
                return false;
            }

            case DictionaryValue dictionary:
            {
                var changed = false;
                var newElements = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>(dictionary.Elements.Count);
                foreach (var entry in dictionary.Elements)
                {
                    if (entry.Key.Value is string keyName && IsSensitivePropertyName(keyName))
                    {
                        newElements.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                            entry.Key,
                            new ScalarValue(RedactedValue)));
                        changed = true;
                    }
                    else if (TryRedactNestedValue(entry.Value, out var valRedacted))
                    {
                        newElements.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                            entry.Key,
                            valRedacted));
                        changed = true;
                    }
                    else
                    {
                        newElements.Add(entry);
                    }
                }

                if (changed)
                {
                    redacted = new DictionaryValue(newElements);
                    return true;
                }

                redacted = value;
                return false;
            }

            default:
                redacted = value;
                return false;
        }
    }

    public static bool IsSensitivePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        if (SensitivePropertyNames.Contains(propertyName))
        {
            return true;
        }

        // Catch compound names like "UserPassword", "BearerToken", "SeqApiKey",
        // while allowing safe non-secret identifiers such as "TokenFamilyId".
        if (propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "CancellationToken", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Authorization", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Cookie", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("NationalNo", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Phone", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySanitizeRequestPath(string rawPath, out string safePath)
    {
        const string nationalPrefix = "/api/People/national/";
        if (rawPath.StartsWith(nationalPrefix, StringComparison.OrdinalIgnoreCase) &&
            rawPath.Length > nationalPrefix.Length)
        {
            safePath = "/api/People/national/{nationalNo}";
            return true;
        }

        const string usernamePrefix = "/api/Users/username/";
        if (rawPath.StartsWith(usernamePrefix, StringComparison.OrdinalIgnoreCase) &&
            rawPath.Length > usernamePrefix.Length)
        {
            safePath = "/api/Users/username/{username}";
            return true;
        }

        safePath = string.Empty;
        return false;
    }
}
