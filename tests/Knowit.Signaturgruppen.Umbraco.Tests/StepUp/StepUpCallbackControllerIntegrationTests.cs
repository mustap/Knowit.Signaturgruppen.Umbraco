using System.Net;
using Knowit.Signaturgruppen.Umbraco.Constants;
using Knowit.Signaturgruppen.Umbraco.StepUp;
using Knowit.Signaturgruppen.Umbraco.StepUp.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Knowit.Signaturgruppen.Umbraco.Tests.StepUp;

public class StepUpCallbackControllerIntegrationTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();
                builder.ConfigureServices(services =>
                {
                    services
                        .AddMvcCore()
                        .AddApplicationPart(typeof(StepUpCallbackController).Assembly);
                });
                builder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapControllers());
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Redirects_to_local_returnUrl_when_query_is_valid()
    {
        var resp = await SendAsync("/members/profile");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/members/profile", resp.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Redirects_to_root_when_returnUrl_query_is_missing()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, SignaturgruppenDefaults.StepUpCallbackPath);

        var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/", resp.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Redirects_to_root_when_returnUrl_is_external()
    {
        var resp = await SendAsync("https://evil.test/phish");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/", resp.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Redirects_to_root_when_returnUrl_is_protocol_relative()
    {
        var resp = await SendAsync("//evil.test/phish");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/", resp.Headers.Location?.OriginalString);
    }

    private async Task<HttpResponseMessage> SendAsync(string returnUrl)
    {
        var url = QueryHelpers.AddQueryString(
            SignaturgruppenDefaults.StepUpCallbackPath,
            SignaturgruppenStepUpService.ReturnUrlQueryKey,
            returnUrl);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        return await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
    }
}
