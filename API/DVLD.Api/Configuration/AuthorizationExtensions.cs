namespace DVLD.Api.Configuration;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddDvldAuthorization(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "AdminOnly",
                policy => policy.RequireRole("Admin"));

            options.AddPolicy(
                "StaffOnly",
                policy => policy.RequireRole("Staff"));

            options.AddPolicy(
                "StaffOrAdmin",
                policy => policy.RequireRole("Staff", "Admin"));
        });

        return services;
    }
}
