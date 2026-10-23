using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.IntegrationTests;

/// <summary>Which X-Forwarded-For values the API honours decides which address the password-recovery throttle charges. Only a
/// proxy listed in <c>Proxy:KnownProxies</c> may speak for the client; anyone else's header is ignored, so it cannot be spoofed to
/// dodge the per-address budget.</summary>
public class ForwardedHeadersTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Recover = "/api/v1/auth/password-recovery/authenticator/verify";
    private const string TrustedProxy = "10.9.9.9";
    private const string UntrustedCaller = "10.1.1.1";

    private readonly TaskFlowApiFactory _factory;

    public ForwardedHeadersTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>A host where the connection's remote address comes from a test header (TestServer has none) and the given
    /// proxies are trusted.</summary>
    private HttpClient CreateClient(params string[] knownProxies)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = knownProxies.Select((proxy, index) => KeyValuePair.Create($"Proxy:KnownProxies:{index}", (string?)proxy));
                configuration.AddInMemoryCollection(settings);
            });
            builder.ConfigureTestServices(services => services.AddTransient<IStartupFilter, RemoteAddressFromHeaderFilter>());
        }).CreateClient();
    }

    private static async Task<HttpStatusCode> GuessAsync(HttpClient client, string remote, string forwardedFor, int index)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Recover)
        {
            // A new made-up name each time, so only the address budget can refuse it.
            Content = JsonContent.Create(new { identifier = $"ghost-{forwardedFor}-{index}", code = "123456" })
        };
        request.Headers.Add(RemoteAddressFromHeaderFilter.Header, remote);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task A_spoofed_forwarded_header_from_an_untrusted_caller_is_ignored()
    {
        var client = CreateClient(TrustedProxy);
        var statuses = new List<HttpStatusCode>();

        // Every request claims to be a different client, but they all come from the same untrusted address.
        for (var i = 0; i < 25; i++)
        {
            statuses.Add(await GuessAsync(client, UntrustedCaller, $"203.0.113.{i + 1}", i));
        }

        Assert.All(statuses.Take(20), status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.All(statuses.Skip(20), status => Assert.Equal(HttpStatusCode.TooManyRequests, status));
    }

    [Fact]
    public async Task A_trusted_proxy_speaks_for_the_real_client_so_clients_do_not_share_a_budget()
    {
        var client = CreateClient(TrustedProxy);

        // Two real clients behind the same proxy, each using up its own 20.
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, await GuessAsync(client, TrustedProxy, "203.0.113.1", i));
            Assert.Equal(HttpStatusCode.BadRequest, await GuessAsync(client, TrustedProxy, "203.0.113.2", i));
        }

        // The first client is now over its own budget; a third client is not affected.
        Assert.Equal(HttpStatusCode.TooManyRequests, await GuessAsync(client, TrustedProxy, "203.0.113.1", 99));
        Assert.Equal(HttpStatusCode.BadRequest, await GuessAsync(client, TrustedProxy, "203.0.113.3", 0));
    }

    [Fact]
    public async Task Without_a_configured_proxy_the_header_is_not_trusted_even_from_the_would_be_proxy_address()
    {
        var client = CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 25; i++)
        {
            statuses.Add(await GuessAsync(client, TrustedProxy, $"203.0.113.{i + 1}", i));
        }

        Assert.Equal(5, statuses.Count(status => status == HttpStatusCode.TooManyRequests));
    }

    private sealed class RemoteAddressFromHeaderFilter : IStartupFilter
    {
        public const string Header = "X-Test-Remote";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, following) =>
                {
                    if (context.Request.Headers.TryGetValue(Header, out var value) && IPAddress.TryParse(value.ToString(), out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await following();
                });
                next(app);
            };
    }
}
