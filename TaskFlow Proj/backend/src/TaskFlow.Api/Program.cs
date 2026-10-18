using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api;
using TaskFlow.Api.Authentication;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Enums (Role, TaskStatus, TaskPriority) travel as their names, matching the SPA's string unions.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
builder.Services.AddScoped<TaskFlow.Application.Workload.IWorkloadService, TaskFlow.Infrastructure.Services.WorkloadService>();
builder.Services.AddScoped<TaskFlow.Application.Reports.IReportService, TaskFlow.Infrastructure.Services.ReportService>();
builder.Services.AddScoped<TaskFlow.Application.Tasks.ITaskService, TaskFlow.Infrastructure.Services.TaskService>();
builder.Services.AddScoped<TaskFlow.Application.Tasks.ITaskCommentService, TaskFlow.Infrastructure.Services.TaskCommentService>();
builder.Services.AddScoped<TaskFlow.Application.Tasks.ITaskActivityService, TaskFlow.Infrastructure.Services.TaskActivityService>();
builder.Services.AddScoped<TaskFlow.Application.Tasks.INotificationPublisher, TaskFlow.Infrastructure.Services.NotificationPublisher>();
builder.Services.AddScoped<TaskFlow.Application.Notifications.INotificationService, TaskFlow.Infrastructure.Services.NotificationService>();
builder.Services.AddScoped<TaskFlow.Application.Reminders.IReminderService, TaskFlow.Infrastructure.Services.ReminderService>();
builder.Services.Configure<TaskFlow.Infrastructure.Services.ReminderEngineOptions>(
    builder.Configuration.GetSection(TaskFlow.Infrastructure.Services.ReminderEngineOptions.SectionName));
builder.Services.AddHostedService<TaskFlow.Infrastructure.Services.ReminderSchedulerService>();
builder.Services.AddScoped<IAuthorizationHandler, TaskAccessHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.Configure<TaskFlow.Infrastructure.Settings.ApplicationOptions>(
    builder.Configuration.GetSection(TaskFlow.Infrastructure.Settings.ApplicationOptions.SectionName));
builder.Services.AddScoped<TaskFlow.Application.Settings.IAppClock, TaskFlow.Infrastructure.Settings.AppClock>();
builder.Services.AddScoped<TaskFlow.Application.Settings.IReminderSettingsService, TaskFlow.Infrastructure.Settings.ReminderSettingsService>();
builder.Services.AddScoped<TaskFlow.Application.Settings.IApplicationSettingsService, TaskFlow.Infrastructure.Settings.ApplicationSettingsService>();
builder.Services.AddScoped<TaskFlow.Application.Mail.IMailService, TaskFlow.Infrastructure.Services.MailService>();
builder.Services.AddScoped<TaskFlow.Application.Mail.IMailAttachmentService, TaskFlow.Infrastructure.Services.MailAttachmentService>();
builder.Services.AddSingleton<TaskFlow.Application.Mail.IMailFileStore, TaskFlow.Infrastructure.Mail.LocalMailFileStore>();
builder.Services.Configure<TaskFlow.Infrastructure.Mail.MailOptions>(
    builder.Configuration.GetSection(TaskFlow.Infrastructure.Mail.MailOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Settings.SettingsInitializer>();
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
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = SessionValidation.ValidateAsync
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.AdminOnly, policy =>
        policy.RequireRole(nameof(Role.Administrator)));

    options.AddPolicy(AuthPolicies.ManagerOrAdmin, policy =>
        policy.RequireRole(nameof(Role.Administrator), nameof(Role.Manager)));

    options.AddPolicy(AuthPolicies.TaskAccess, policy =>
        policy.Requirements.Add(new TaskAccessRequirement()));
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
    await scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Settings.SettingsInitializer>().InitializeAsync();

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
