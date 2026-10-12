using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(DatabasePathResolver.Resolve(builder.Configuration.GetConnectionString("Default"))));

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<TaskFlow.Application.Departments.IDepartmentService, TaskFlow.Infrastructure.Services.DepartmentService>();
builder.Services.AddScoped<TaskFlow.Application.Users.IUserService, TaskFlow.Infrastructure.Services.UserService>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.IDashboardService, TaskFlow.Infrastructure.Services.DashboardService>();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.AdminOnly, policy =>
        policy.RequireRole(nameof(Role.Administrator)));

    options.AddPolicy(AuthPolicies.ManagerOrAdmin, policy =>
        policy.RequireRole(nameof(Role.Administrator), nameof(Role.Manager)));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.SpaClient, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapGet("/api/v1/diagnostics/throw", (Func<IResult>)(() => throw new InvalidOperationException("Diagnostic failure.")));
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(CorsPolicies.SpaClient);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
