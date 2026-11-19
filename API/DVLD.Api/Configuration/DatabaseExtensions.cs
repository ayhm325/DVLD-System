using Application.Interfaces;

using Infrastructure;
using Infrastructure.Configuration;

using Microsoft.EntityFrameworkCore;

namespace DVLD.Api.Configuration;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDvldPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString =
            configuration.GetConnectionString("DVLDConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DVLDConnection' was not found.");

        // Fail-fast test-host safety gate. A host running with the "Testing" environment
        // may only target an isolated, generated test database; DVLD and DVLDf are rejected
        // outright. This validates the connection string only and never opens a connection.
        SqlServerConnectionSafety.EnsureSafeForHost(
            connectionString,
            environment.EnvironmentName);

        services.AddDbContext<DVLDDbContext>(
            options =>
                options.UseSqlServer(
                    connectionString,
                    sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
