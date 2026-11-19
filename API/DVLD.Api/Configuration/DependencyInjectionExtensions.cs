using Application.Interfaces;
using Application.Options;
using Application.Services;

using DVLD.Api.Security;

using Infrastructure.Repositories;
using Infrastructure.Services;

namespace DVLD.Api.Configuration;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddDvldApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // Repositories
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IApplicationTypeRepository, ApplicationTypeRepository>();
        services.AddScoped<ICountryRepository, CountryRepository>();
        services.AddScoped<IDetainedLicenseRepository, DetainedLicenseRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<ILicenseClassRepository, LicenseClassRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();

        services.AddScoped<
            ILocalDrivingLicenseApplicationRepository,
            LocalDrivingLicenseApplicationRepository>();

        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<
            ITestAppointmentRepository,
            TestAppointmentRepository>();

        services.AddScoped<ITestRepository, TestRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ITestTypeRepository, TestTypeRepository>();
        services.AddScoped<IInternationalRepository, InternationalRepository>();

        // Application Services & Infrastructure Helpers
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ICurrentUserService, ApiCurrentUserService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IApplicationTypeService, ApplicationTypeService>();
        services.AddScoped<ICountryService, CountryService>();
        services.AddScoped<IDetainedLicenseService, DetainedLicenseService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<ILicenseClassService, LicenseClassService>();
        services.AddScoped<
            ILocalDrivingLicenseApplicationService,
            LocalDrivingLicenseApplicationService>();

        services.AddScoped<IPersonService, PersonService>();
        services.AddSingleton<IFileStorageService, PhysicalFileStorageService>();
        services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        services.AddSingleton<IJordanClock, JordanClock>();
        services.AddOptions<NoShowProcessorOptions>()
            .Bind(configuration.GetSection(NoShowProcessorOptions.SectionName));
        services.AddScoped<INoShowProcessorService, NoShowProcessorService>();
        services.AddHostedService<DVLD.Api.BackgroundServices.NoShowBackgroundService>();
        services.AddOptions<LicenseExpirationOptions>()
            .Bind(configuration.GetSection(LicenseExpirationOptions.SectionName));
        services.AddHostedService<DVLD.Api.BackgroundServices.LicenseExpirationBackgroundService>();

        services.AddScoped<
            ITestAppointmentService,
            TestAppointmentService>();

        services.AddScoped<ITestService, TestService>();
        services.AddScoped<ITestTypeService, TestTypeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IInternationalService, InternationalService>();

        // Authentication & License Workflows
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddOptions<AuthSessionOptions>()
            .Bind(configuration.GetSection(AuthSessionOptions.SectionName))
            .Validate(
                options =>
                    options.RefreshTokenLifetimeDays is >= 1 and <= 90 &&
                    options.IdleTimeoutDays is >= 1 and <= 30 &&
                    options.IdleTimeoutDays <= options.RefreshTokenLifetimeDays &&
                    options.RotationGraceSeconds is >= 0 and <= 300 &&
                    !string.IsNullOrWhiteSpace(options.RefreshCookieName) &&
                    options.RefreshCookiePath.StartsWith('/'),
                "AuthSession configuration is invalid: refresh lifetime must be 1-90 days, idle timeout 1-30 days and not greater than the refresh lifetime, rotation grace 0-300 seconds, and the cookie name/path must be configured.")
            .ValidateOnStart();

        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<
            ILicenseRenewalService,
            LicenseRenewalService>();

        services.AddScoped<
            ILicenseIssuanceService,
            LicenseIssuanceService>();

        services.AddScoped<
            ILicenseReplacementService,
            LicenseReplacementService>();

        services.AddScoped<
            ITestWorkflowService,
            TestWorkflowService>();

        services.AddScoped<
            ILicenseQueryService,
            LicenseQueryService>();

        return services;
    }
}
