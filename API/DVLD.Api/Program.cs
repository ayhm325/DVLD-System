using DVLD.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

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

public partial class Program { }
