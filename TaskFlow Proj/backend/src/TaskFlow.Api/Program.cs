using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
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
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Persistence.Seeders;

var builder = WebApplication.CreateBuilder(args);

// Per-environment values (CORS, mail links, seeding) live in this one file; environment variables still override it.
builder.Configuration.AddJsonFile("appsettings.Environment.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Enums (Role, TaskStatus, TaskPriority) travel as their names, matching the SPA's string unions.
builder.Services.AddControllers(options => options.Filters.Add<TaskFlow.Api.Filters.ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TaskFlow.Api.Middleware.GlobalExceptionHandler>();

// The real client address matters to the password-recovery throttle. Only loopback proxies are trusted unless the deployment
// lists its own in Proxy:KnownProxies, so a caller cannot spoof X-Forwarded-For.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor;
    foreach (var proxy in builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (System.Net.IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

// Resolved when the context is first built (not while this file runs) so configuration added later, such as by the
// integration tests, decides where the database goes.
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
    options.UseSqlite(DatabasePathResolver.Resolve(
        provider.GetRequiredService<IConfiguration>().GetConnectionString("Default"),
        StorageRoot.Resolve(provider.GetRequiredService<IOptions<StorageOptions>>().Value.Root))));

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<TaskFlow.Application.Projects.IProjectService, TaskFlow.Infrastructure.Services.ProjectService>();
builder.Services.AddScoped<TaskFlow.Application.Users.IUserAllocationService, TaskFlow.Infrastructure.Services.UserAllocationService>();
builder.Services.AddScoped<TaskFlow.Application.Projects.IPositionService, TaskFlow.Infrastructure.Services.PositionService>();
builder.Services.AddScoped<TaskFlow.Application.Departments.IDepartmentService, TaskFlow.Infrastructure.Services.DepartmentService>();
builder.Services.AddScoped<TaskFlow.Application.Users.IUserService, TaskFlow.Infrastructure.Services.UserService>();
builder.Services.AddScoped<TaskFlow.Application.Roles.IRoleService, TaskFlow.Infrastructure.Services.RoleService>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.IDashboardService, TaskFlow.Infrastructure.Services.DashboardService>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IDashboardScope, TaskFlow.Infrastructure.Analytics.AdminDashboardScope>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IManagerDashboardScope, TaskFlow.Infrastructure.Analytics.ManagerDashboardScope>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IManagerAnalyticsService, TaskFlow.Infrastructure.Analytics.ManagerAnalyticsService>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IAdminAnalyticsService, TaskFlow.Infrastructure.Analytics.AdminAnalyticsService>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IEmployeeDashboardScope, TaskFlow.Infrastructure.Analytics.EmployeeDashboardScope>();
builder.Services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IEmployeeAnalyticsService, TaskFlow.Infrastructure.Analytics.EmployeeAnalyticsService>();
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

// Password recovery from the login page: an authenticator code buys a single-use reset token.
builder.Services.AddScoped<TaskFlow.Application.Auth.IPasswordRecoveryService, TaskFlow.Application.Auth.PasswordRecoveryService>();
builder.Services.AddSingleton<TaskFlow.Application.Auth.IAttemptLimiter, TaskFlow.Infrastructure.Services.AttemptLimiter>();

// Authenticator-app MFA (free TOTP). The key is encrypted with the Data Protection key ring.
builder.Services.Configure<TaskFlow.Application.Mfa.MfaOptions>(
    builder.Configuration.GetSection(TaskFlow.Application.Mfa.MfaOptions.SectionName));
builder.Services.AddSingleton<TaskFlow.Application.Mfa.IMfaSecretProtector, TaskFlow.Infrastructure.Services.MfaSecretProtector>();
builder.Services.AddScoped<TaskFlow.Application.Mfa.IMfaService, TaskFlow.Application.Mfa.MfaService>();
builder.Services.AddSingleton<TaskFlow.Application.Mail.IMailFileStore, TaskFlow.Infrastructure.Mail.LocalMailFileStore>();
builder.Services.Configure<TaskFlow.Infrastructure.Mail.MailOptions>(
    builder.Configuration.GetSection(TaskFlow.Infrastructure.Mail.MailOptions.SectionName));

builder.Services.Configure<TaskFlow.Infrastructure.Assistant.AiProviderOptions>(
    builder.Configuration.GetSection(TaskFlow.Infrastructure.Assistant.AiProviderOptions.SectionName));
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("TaskFlow");
if (OperatingSystem.IsWindows())
{
    // The key ring then only decrypts for the Windows account that runs the API, so a copied folder is not enough.
    dataProtection.ProtectKeysWithDpapi();
}
// The key ring folder is read from the bound options when Data Protection is first used (not while this file runs), so
// configuration added later, such as by the integration tests, decides where the keys go.
builder.Services
    .AddOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>()
    .Configure<Microsoft.Extensions.Options.IOptions<TaskFlow.Infrastructure.Assistant.AiProviderOptions>, Microsoft.Extensions.Options.IOptions<StorageOptions>, ILoggerFactory>(
        (options, aiOptions, storage, loggerFactory) =>
            options.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(
                new DirectoryInfo(StorageRoot.ResolveUnder(StorageRoot.Resolve(storage.Value.Root), aiOptions.Value.KeyRingPath)),
                loggerFactory));
builder.Services.AddSingleton<TaskFlow.Application.Assistant.IAiKeyProtector, TaskFlow.Infrastructure.Assistant.AiKeyProtector>();
// The timeout is read from the bound options when a client is created. Provider clients never log request or response headers or bodies, so keys and prompts stay out of the logs.
// Redirects are not followed: the address an administrator saved is the only one that is called, so a redirect cannot
// lead the server on to another (for example a cloud metadata) address.
builder.Services.AddHttpClient<TaskFlow.Infrastructure.Assistant.OllamaProvider>(ConfigureAiClient)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddHttpClient<TaskFlow.Infrastructure.Assistant.GeminiProvider>(ConfigureAiClient)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddHttpClient<TaskFlow.Infrastructure.Assistant.OpenAiProvider>(ConfigureAiClient)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddTransient<TaskFlow.Application.Assistant.IChatModelProvider>(sp => sp.GetRequiredService<TaskFlow.Infrastructure.Assistant.OllamaProvider>());
builder.Services.AddTransient<TaskFlow.Application.Assistant.IChatModelProvider>(sp => sp.GetRequiredService<TaskFlow.Infrastructure.Assistant.GeminiProvider>());
builder.Services.AddTransient<TaskFlow.Application.Assistant.IChatModelProvider>(sp => sp.GetRequiredService<TaskFlow.Infrastructure.Assistant.OpenAiProvider>());
builder.Services.AddScoped<TaskFlow.Application.Assistant.IChatModelProviderRegistry, TaskFlow.Infrastructure.Assistant.ChatModelProviderRegistry>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAiProviderConfigService, TaskFlow.Infrastructure.Assistant.AiProviderConfigService>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IChatModelGateway, TaskFlow.Infrastructure.Assistant.ChatModelGateway>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAiProviderConnectionTester, TaskFlow.Infrastructure.Assistant.AiProviderConnectionTester>();
builder.Services.AddSingleton<TaskFlow.Application.Assistant.IAssistantRateLimiter, TaskFlow.Infrastructure.Assistant.AssistantRateLimiter>();
builder.Services.AddSingleton<TaskFlow.Application.Assistant.IHelpKnowledgeBase, TaskFlow.Infrastructure.Assistant.HelpKnowledgeBase>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAssistantAuthorizer, AssistantAuthorizer>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantTaskReader>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantUserResolver>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantManagerResolver>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantToolGate>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.GetMyTasksTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.PrioritiseTasksTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.SearchHelpTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.FindUserTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ExplainPasswordChangeTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ContactAdminGuidanceTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.DraftMailTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ProposeProfileUpdateTool>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ProposeRoleChangeTool>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAssistantToolbox, TaskFlow.Infrastructure.Assistant.AssistantToolbox>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAssistantAuditLog, TaskFlow.Infrastructure.Assistant.AssistantAuditLog>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAssistantActionService, TaskFlow.Infrastructure.Assistant.AssistantActionService>();
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAssistantActionCards>(
    provider => provider.GetRequiredService<TaskFlow.Application.Assistant.IAssistantActionService>());
builder.Services.AddScoped<TaskFlow.Application.Assistant.IAssistantService, TaskFlow.Infrastructure.Assistant.AssistantService>();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddScoped<AccessControlSeeder>();
builder.Services.AddScoped<TaskFlow.Infrastructure.Assistant.AiProviderSeeder>();
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
    // One policy per catalog permission; the permission claims are refreshed from the database on every request.
    foreach (var permission in TaskFlow.Api.Common.Security.PermissionCatalog.All)
    {
        options.AddPolicy(
            TaskFlow.Api.Common.Security.PermissionPolicy.NameFor(permission.Key),
            policy => policy.RequireClaim(AuthClaims.Permission, permission.Key));
    }

    options.AddPolicy(AuthPolicies.TaskAccess, policy =>
        policy.Requirements.Add(new TaskAccessRequirement()));
});

// Refused authorizations are recorded as denied actions in the audit trail - identifiers only,
// never a credential (Requirement 18's unauthorized-change scenario).
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, DeniedActionAuditHandler>();

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


// The data folder must exist and be writable before the database, key ring or attachments are touched.
var storageRoot = StorageRoot.Resolve(app.Services.GetRequiredService<IOptions<StorageOptions>>().Value.Root);
StorageRoot.EnsureAvailable(storageRoot);

// Checked before the migration below creates the new database, so an upgraded install is told why it starts empty.
if (StorageRoot.FindLegacyDatabase(storageRoot, DatabasePathResolver.FindSolutionDirectory()) is { } legacyDatabase)
{
    app.Logger.LogWarning(
        "Found an earlier database at {LegacyDatabase}, but TaskFlow now keeps its data in {StorageRoot} and has no database there yet, "
        + "so it will start empty. The old data is not migrated: stop the API and copy taskflow.db (and the dp-keys and "
        + "mail-attachments folders next to it) into {StorageRoot}, or set Storage:Root to the old folder.",
        legacyDatabase,
        storageRoot);
}

if (OperatingSystem.IsWindows())
{
    var keyRingFolder = StorageRoot.ResolveUnder(
        storageRoot,
        app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaskFlow.Infrastructure.Assistant.AiProviderOptions>>().Value.KeyRingPath);
    var keyRingCheck = TaskFlow.Infrastructure.Assistant.KeyRingProtectionCheck.Check(keyRingFolder);
    if (keyRingCheck.UnprotectedKeys > 0)
    {
        app.Logger.LogWarning(
            "The AI key ring in {KeyRingFolder} has {Count} key(s) stored unprotected, from before DPAPI protection was turned on. "
            + "To protect it: stop the API, delete that folder, start the API and enter the AI provider API keys again.",
            keyRingFolder,
            keyRingCheck.UnprotectedKeys);
    }

    if (keyRingCheck.CouldNotCheckReason is { } reason)
    {
        app.Logger.LogInformation(
            "The AI key ring in {KeyRingFolder} could not be fully checked for unprotected keys: {Reason}. "
            + "If it was created before DPAPI protection was turned on, it may still hold an unprotected key.",
            keyRingFolder,
            reason);
    }
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Settings.SettingsInitializer>().InitializeAsync();

    await scope.ServiceProvider.GetRequiredService<AccessControlSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Assistant.AiProviderSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapGet("/api/v1/diagnostics/throw", (Func<IResult>)(() => throw new InvalidOperationException("Diagnostic failure.")));
}

app.UseForwardedHeaders();
// Outside the exception handler, so the entry records the final status of a request that failed.
app.UseMiddleware<TaskFlow.Api.Middleware.RequestLoggingMiddleware>();
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(CorsPolicies.SpaClient);

app.UseAuthentication();
app.UseMiddleware<TaskFlow.Api.Authentication.MfaSetupGateMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
    private static void ConfigureAiClient(IServiceProvider services, HttpClient client) =>
        client.Timeout = TimeSpan.FromSeconds(
            services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaskFlow.Infrastructure.Assistant.AiProviderOptions>>().Value.TimeoutSeconds);
}
