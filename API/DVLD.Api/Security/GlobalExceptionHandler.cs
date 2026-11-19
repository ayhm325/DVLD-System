using System.Text.Json;
using System.Text.RegularExpressions;

using Application.Common.Exceptions;

using DVLD.Api.Configuration;
using DVLD.Api.Results;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace DVLD.Api.Security;

/// <summary>
/// Centralized exception boundary for the API. Every unhandled exception becomes an
/// RFC 7807 <see cref="ProblemDetails"/> response; internal details are never exposed.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(exception);

        // A cancellation requested by the client is not a server error. Never turn it
        // into a generic 500, and never attempt to log it as one.
        if (exception is OperationCanceledException &&
            context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request {Method} {Route} was canceled by the client (RequestId: {RequestId}).",
                context.Request.Method,
                LoggingExtensions.GetSafeRoute(context),
                context.TraceIdentifier);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode =
                    StatusCodes.Status499ClientClosedRequest;
            }

            return true;
        }

        if (context.Response.HasStarted)
        {
            return false;
        }

        // The client is gone. Never attempt to write a response to a disconnected
        // socket; suppress it and record the underlying exception for diagnosis.
        if (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "The client disconnected before an error response could be written for {Method} {Route} (RequestId: {RequestId}); the response was suppressed.",
                context.Request.Method,
                LoggingExtensions.GetSafeRoute(context),
                context.TraceIdentifier);

            return true;
        }

        var failure = Classify(exception);
        var safeException = SanitizeExceptionForLogging(exception);

        if (failure.LogAsError)
        {
            _logger.LogError(
                safeException,
                "Unhandled exception occurred while processing {Method} {Route} (RequestId: {RequestId}).",
                context.Request.Method,
                LoggingExtensions.GetSafeRoute(context),
                context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                safeException,
                "Rejected request while processing {Method} {Route} (RequestId: {RequestId}).",
                context.Request.Method,
                LoggingExtensions.GetSafeRoute(context),
                context.TraceIdentifier);
        }

        var problemDetails = ApiProblemDetails.Create(
            context,
            failure.StatusCode,
            failure.Title,
            failure.Detail,
            failure.Type);

        await WriteProblemDetailsAsync(context, problemDetails, cancellationToken);

        return true;
    }

    private static ExceptionFailure Classify(Exception exception) =>
        exception switch
        {
            // Malformed request bodies, invalid content lengths, oversized payloads.
            // MVC usually converts these into a 400 before the pipeline, but the
            // framework can also raise them directly.
            BadHttpRequestException badRequest => new(
                NormalizeBadRequestStatus(badRequest.StatusCode),
                ReasonPhrases.GetReasonPhrase(
                    NormalizeBadRequestStatus(badRequest.StatusCode)),
                "The request could not be processed.",
                ProblemTypes.ForStatusCode(
                    NormalizeBadRequestStatus(badRequest.StatusCode)),
                LogAsError: false),

            JsonException => new(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "The request body is not valid JSON.",
                ProblemTypes.Validation,
                LogAsError: false),

            // Curated, client-safe domain conflicts translated at the persistence
            // boundary. Their messages are intentional API text, not internals.
            DuplicateUsernameException duplicateUsername => new(
                StatusCodes.Status409Conflict,
                "Conflict",
                duplicateUsername.Message,
                ProblemTypes.Conflict,
                LogAsError: false),

            DuplicatePersonUserException duplicatePersonUser => new(
                StatusCodes.Status409Conflict,
                "Conflict",
                duplicatePersonUser.Message,
                ProblemTypes.Conflict,
                LogAsError: false),

            ConcurrencyConflictException concurrencyConflict => new(
                StatusCodes.Status409Conflict,
                "Conflict",
                concurrencyConflict.Message,
                ProblemTypes.Conflict,
                LogAsError: false),

            _ => new ExceptionFailure(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "The server could not complete the request.",
                ProblemTypes.Internal,
                LogAsError: true)
        };

    private static int NormalizeBadRequestStatus(int statusCode) =>
        statusCode is StatusCodes.Status400BadRequest
            or StatusCodes.Status413PayloadTooLarge
            ? statusCode
            : StatusCodes.Status400BadRequest;

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        ProblemDetails problemDetails,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode =
            problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        try
        {
            await context.Response.WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: ApiProblemDetails.ContentType,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The client disconnected while the error response was being written.
        }
        catch (IOException)
        {
            // The connection was closed before the response could be written.
        }
    }

    private sealed record ExceptionFailure(
        int StatusCode,
        string Title,
        string Detail,
        string Type,
        bool LogAsError);

    private static readonly Regex KeyValueSecretRegex = new(
        @"(?i)\b(Password|Pwd|Secret|SecretKey|ApiKey|AccessToken|RefreshToken|Token)\s*=\s*[^;\s]+",
        RegexOptions.Compiled);

    private static readonly Regex BearerTokenRegex = new(
        @"(?i)\bBearer\s+[A-Za-z0-9\-_\.=]+",
        RegexOptions.Compiled);

    public static Exception SanitizeExceptionForLogging(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (!ContainsSensitiveText(exception))
        {
            return exception;
        }

        var sanitizedMessage = ScrubText(exception.Message);
        var innerSummary = exception.InnerException is not null
            ? $" | Inner({exception.InnerException.GetType().Name}): {ScrubText(exception.InnerException.Message)}"
            : string.Empty;

        return new SanitizedDiagnosticException(
            $"[{exception.GetType().Name}] {sanitizedMessage}{innerSummary}",
            exception.StackTrace);
    }

    private static bool ContainsSensitiveText(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (KeyValueSecretRegex.IsMatch(current.Message) ||
                BearerTokenRegex.IsMatch(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static string ScrubText(string text)
    {
        var scrubbed = KeyValueSecretRegex.Replace(text, "$1=[REDACTED]");
        return BearerTokenRegex.Replace(scrubbed, "Bearer [REDACTED]");
    }

    private sealed class SanitizedDiagnosticException(string message, string? originalStackTrace)
        : Exception(message)
    {
        public override string? StackTrace => originalStackTrace;
    }
}
