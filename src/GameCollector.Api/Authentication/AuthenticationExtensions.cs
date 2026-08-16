using System.Security.Claims;
using GameCollector.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Auditing;
using GameCollector.Api.Auditing;

namespace GameCollector.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetRequiredSection(KeycloakOptions.SectionName);
        var keycloakOptions = section.Get<KeycloakOptions>()
            ?? throw new InvalidOperationException("Keycloak authentication configuration is missing.");

        services.AddOptions<KeycloakOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority.TrimEnd('/');
                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            });

        services.AddTransient<IClaimsTransformation, KeycloakRoleClaimsTransformation>();
        services.AddAuthorizationBuilder()
            .AddPolicy(
                AuthorizationPolicies.Administrator,
                policy =>
                {
                    policy.RequireRole(keycloakOptions.AdminRole);
                    policy.AddRequirements(new EnabledApplicationUserRequirement());
                })
            .AddPolicy(
                AuthorizationPolicies.ActiveDevice,
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new ActiveDeviceRequirement());
                });
        services.AddScoped<IAuthorizationHandler, ActiveDeviceAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, EnabledApplicationUserAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

        return services;
    }

    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IAuditContext, HttpAuditContext>();
        return services;
    }
}
