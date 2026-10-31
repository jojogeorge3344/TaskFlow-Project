using TaskFlow.Api.Integrations.Security;
using TaskFlow.Api.Services.Assistant;
using TaskFlow.Api.Integrations.Ai;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Config;

/// <summary>Adapters to systems outside the application: token signing, password hashing, the Data Protection key ring, the
/// mail attachment store and the AI providers.</summary>
public static class IntegrationRegistration
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        // Authenticator-app MFA (free TOTP). The key is encrypted with the Data Protection key ring.
        services.AddSingleton<TaskFlow.Api.Integrations.Security.IMfaSecretProtector, TaskFlow.Api.Integrations.Security.MfaSecretProtector>();

        services.AddSingleton<TaskFlow.Api.Integrations.Storage.IMailFileStore, TaskFlow.Api.Integrations.Storage.LocalMailFileStore>();
        services.Configure<TaskFlow.Api.Config.MailOptions>(configuration.GetSection(TaskFlow.Api.Config.MailOptions.SectionName));

        services.Configure<AiProviderOptions>(configuration.GetSection(AiProviderOptions.SectionName));
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("TaskFlow");
        if (OperatingSystem.IsWindows())
        {
            // The key ring then only decrypts for the Windows account that runs the API, so a copied folder is not enough.
            dataProtection.ProtectKeysWithDpapi();
        }

        // The key ring folder is read from the bound options when Data Protection is first used (not while the app starts),
        // so configuration added later, such as by the integration tests, decides where the keys go.
        services
            .AddOptions<KeyManagementOptions>()
            .Configure<IOptions<AiProviderOptions>, IOptions<StorageOptions>, ILoggerFactory>(
                (options, aiOptions, storage, loggerFactory) =>
                    options.XmlRepository = new FileSystemXmlRepository(
                        new DirectoryInfo(StorageRoot.ResolveUnder(StorageRoot.Resolve(storage.Value.Root), aiOptions.Value.KeyRingPath)),
                        loggerFactory));
        services.AddSingleton<TaskFlow.Api.Integrations.Ai.IAiKeyProtector, AiKeyProtector>();

        // The timeout is read from the bound options when a client is created. Provider clients never log request or response
        // headers or bodies, so keys and prompts stay out of the logs. Redirects are not followed: the address an administrator
        // saved is the only one that is called, so a redirect cannot lead the server on to another (for example a cloud
        // metadata) address.
        services.AddHttpClient<OllamaProvider>(ConfigureAiClient)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.AddHttpClient<GeminiProvider>(ConfigureAiClient)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.AddHttpClient<OpenAiProvider>(ConfigureAiClient)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.AddTransient<TaskFlow.Api.Integrations.Ai.IChatModelProvider>(sp => sp.GetRequiredService<OllamaProvider>());
        services.AddTransient<TaskFlow.Api.Integrations.Ai.IChatModelProvider>(sp => sp.GetRequiredService<GeminiProvider>());
        services.AddTransient<TaskFlow.Api.Integrations.Ai.IChatModelProvider>(sp => sp.GetRequiredService<OpenAiProvider>());
        services.AddScoped<TaskFlow.Api.Integrations.Ai.IChatModelProviderRegistry, ChatModelProviderRegistry>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.IChatModelGateway, ChatModelGateway>();

        return services;
    }

    private static void ConfigureAiClient(IServiceProvider services, HttpClient client) =>
        client.Timeout = TimeSpan.FromSeconds(services.GetRequiredService<IOptions<AiProviderOptions>>().Value.TimeoutSeconds);
}
