using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api.Authentication;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Filters;
using TaskFlow.Api.Middleware;
using TaskFlow.Application.Auth;
using TaskFlow.Infrastructure.Authentication;

namespace TaskFlow.Api.Config;

/// <summary>Concerns every module shares, set up once: MVC and JSON, error responses, validation, authentication,
/// authorization and CORS.</summary>
public static class CrossCuttingRegistration
{
    public static IServiceCollection AddCrossCutting(this IServiceCollection services, IConfiguration configuration)
    {
        // Enums (Role, TaskStatus, TaskPriority) travel as their names, matching the SPA's string unions.
        services.AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddOpenApi();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddSingleton(TimeProvider.System);

        // The real client address matters to the password-recovery throttle. Only loopback proxies are trusted unless the
        // deployment lists its own in Proxy:KnownProxies, so a caller cannot spoof X-Forwarded-For.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor;
            foreach (var proxy in configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
            {
                if (System.Net.IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = SessionValidation.ValidateAsync
                };
            });

        services.AddAuthorization(options =>
        {
            // One policy per catalog permission; the permission claims are refreshed from the database on every request.
            foreach (var permission in PermissionCatalog.All)
            {
                options.AddPolicy(
                    PermissionPolicy.NameFor(permission.Key),
                    policy => policy.RequireClaim(AuthClaims.Permission, permission.Key));
            }

            options.AddPolicy(AuthPolicies.TaskAccess, policy =>
                policy.Requirements.Add(new TaskAccessRequirement()));
        });
        services.AddScoped<IAuthorizationHandler, TaskAccessHandler>();

        // Refused authorizations are recorded as denied actions in the audit trail - identifiers only, never a credential
        // (Requirement 18's unauthorized-change scenario).
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, DeniedActionAuditHandler>();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicies.SpaClient, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        return services;
    }
}
