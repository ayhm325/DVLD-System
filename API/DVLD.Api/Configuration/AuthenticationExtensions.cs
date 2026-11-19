using System.Text;

using Application.Interfaces;
using Application.Options;

using DVLD.Api.Security;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DVLD.Api.Configuration;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddDvldAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName));

        var jwtOptions =
            configuration
                .GetSection(JwtOptions.SectionName)
                .Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "JWT configuration is missing.");

        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
            throw new InvalidOperationException(
                "JWT SecretKey is required.");

        if (Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
            throw new InvalidOperationException(
                "JWT SecretKey must be at least 32 bytes long.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
            throw new InvalidOperationException(
                "JWT Issuer is required.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
            throw new InvalidOperationException(
                "JWT Audience is required.");

        if (jwtOptions.ExpirationMinutes <= 0)
            throw new InvalidOperationException(
                "JWT ExpirationMinutes must be greater than zero.");

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,

                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;

                        var userIdClaim =
                            principal?
                                .FindFirst(
                                    System.Security.Claims.ClaimTypes.NameIdentifier)?
                                .Value;

                        var securityStampClaim =
                            principal?
                                .FindFirst("sst")?
                                .Value;

                        var sessionIdClaim =
                            principal?
                                .FindFirst("sid")?
                                .Value;

                        if (!int.TryParse(userIdClaim, out var userId) ||
                            userId <= 0 ||
                            string.IsNullOrWhiteSpace(securityStampClaim))
                        {
                            context.Fail("Invalid user identity.");
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(sessionIdClaim) ||
                            !Guid.TryParse(sessionIdClaim, out var sessionId) ||
                            sessionId == Guid.Empty)
                        {
                            context.Fail("Missing or invalid session identifier claim.");
                            return;
                        }

                        var authService =
                            context.HttpContext.RequestServices
                                .GetRequiredService<IAuthService>();

                        // Activation state, security stamp, and active session state are validated
                        // server-side on every request. Password, role, or activation changes rotate
                        // the stamp, and logout revokes the session family, invalidating tokens immediately.
                        var principalIsCurrent = await authService.IsPrincipalCurrentAsync(
                            userId,
                            securityStampClaim,
                            sessionId,
                            context.HttpContext.RequestAborted);

                        if (!principalIsCurrent)
                        {
                            context.Fail(
                                "User account is inactive or its security state has changed.");
                        }
                    }
                };
            });

        return services;
    }
}
