using System.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;

using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace DVLD.Api.Configuration;

public static class LoggingExtensions
{
    public const string DefaultRequestMessageTemplate =
        "HTTP {RequestMethod} {Route} responded {StatusCode} in {Elapsed:0.0000} ms";

    private static int _selfLogInitialized;

    public static void EnsureSelfLogEnabled()
    {
        if (Interlocked.CompareExchange(ref _selfLogInitialized, 1, 0) == 0)
        {
            SelfLog.Enable(message => Console.Error.WriteLine($"[Serilog SelfLog] {message}"));
        }
    }

    public static IHostBuilder UseDvldSerilog(this IHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            ConfigureDvldSerilog(
                loggerConfiguration,
                context.Configuration,
                context.HostingEnvironment);

            foreach (var sink in services.GetServices<ILogEventSink>())
            {
                loggerConfiguration.WriteTo.Sink(sink);
            }
        }, preserveStaticLogger: true);
    }

    public static void ConfigureDvldSerilog(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(loggerConfiguration);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        EnsureSelfLogEnabled();

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "DVLD")
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .Enrich.With<SensitiveDataRedactingEnricher>()
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        ConfigureSeqSink(loggerConfiguration, configuration);
        ConfigureOptionalFileSink(loggerConfiguration, configuration);
    }

    public static WebApplication UseDvldRequestLogging(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSerilogRequestLogging(options =>
        {
            options.Logger = app.Services.GetService<Serilog.ILogger>();
            options.MessageTemplate = DefaultRequestMessageTemplate;

            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                {
                    return LogEventLevel.Error;
                }

                var path = httpContext.Request.Path.Value;
                if (!string.IsNullOrEmpty(path) &&
                    (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)))
                {
                    return LogEventLevel.Debug;
                }

                return LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var safeRoute = GetSafeRoute(httpContext);

                diagnosticContext.Set("HttpMethod", httpContext.Request.Method);
                diagnosticContext.Set("Route", safeRoute);
                // Overwrite RequestPath with the safe route template so raw parameters
                // (such as NationalNo or Username) are never stored in Seq.
                diagnosticContext.Set("RequestPath", safeRoute);
                diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);
                diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
                diagnosticContext.Set(
                    "TraceId",
                    Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier);

                if (Activity.Current is { } activity)
                {
                    diagnosticContext.Set("SpanId", activity.SpanId.ToString());
                }
            };
        });

        return app;
    }

    public static string GetSafeRoute(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var endpoint = httpContext.GetEndpoint()
            ?? httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint;

        if (endpoint is RouteEndpoint routeEndpoint &&
            !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText))
        {
            var raw = routeEndpoint.RoutePattern.RawText;
            return raw.StartsWith('/') ? raw : "/" + raw;
        }

        return "<unmatched>";
    }

    private static void ConfigureSeqSink(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var serverUrl = configuration["Seq:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return;
        }

        if (!Uri.TryCreate(serverUrl.Trim(), UriKind.Absolute, out var seqUri) ||
            (seqUri.Scheme != Uri.UriSchemeHttp && seqUri.Scheme != Uri.UriSchemeHttps))
        {
            SelfLog.WriteLine(
                "DVLD.Api: Invalid Seq:ServerUrl configuration value; Seq sink was not enabled.");
            return;
        }

        var apiKey = configuration["Seq:ApiKey"];

        loggerConfiguration.WriteTo.Seq(
            serverUrl: seqUri.ToString(),
            apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
    }

    private static void ConfigureOptionalFileSink(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("DiagnosticLogging:File:Enabled");
        if (!enabled)
        {
            return;
        }

        var path = configuration["DiagnosticLogging:File:Path"];
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "logs/dvld-.log";
        }

        var retainedFiles = Math.Max(
            1,
            configuration.GetValue<int?>("DiagnosticLogging:File:RetainedFileCountLimit") ?? 7);

        var fileSizeLimitBytes = Math.Max(
            1_048_576L,
            configuration.GetValue<long?>("DiagnosticLogging:File:FileSizeLimitBytes") ?? 10_485_760L);

        try
        {
            loggerConfiguration.WriteTo.File(
                path: path,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: fileSizeLimitBytes,
                retainedFileCountLimit: retainedFiles,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine(
                $"DVLD.Api: Failed to initialize optional file sink: {ex.Message}");
        }
    }
}
