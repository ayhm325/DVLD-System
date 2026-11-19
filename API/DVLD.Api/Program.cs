using DVLD.Api.Configuration;

using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseDvldSerilog();

    builder.Services
        .AddDvldAuthentication(builder.Configuration)
        .AddDvldAuthorization()
        .AddDvldRateLimiting()
        .AddDvldPersistence(builder.Configuration, builder.Environment)
        .AddDvldApplicationServices(builder.Configuration)
        .AddDvldApiAndSwagger();

    var app = builder.Build();

    app.UseDvldPipeline();

    // ============================================================
    // PROGRAM ENTRY POINT
    // ============================================================
    //
    // License expiration is processed exclusively by LicenseExpirationBackgroundService,
    // which performs its first sweep immediately at startup and then on its polling
    // interval. The former inline startup sweep was removed: it duplicated that hosted
    // service and performed a database write whenever a host was constructed — including
    // test hosts — before the pipeline even existed. Tests disable the hosted service
    // through LicenseExpirationOptions.

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "DVLD.Api terminated unexpectedly during startup or host execution.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
