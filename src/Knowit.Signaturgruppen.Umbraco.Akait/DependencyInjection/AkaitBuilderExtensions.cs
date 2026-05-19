using IdentityModel.Client;
using Knowit.Signaturgruppen.Umbraco.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;

namespace Knowit.Signaturgruppen.Umbraco.Akait.DependencyInjection;

public static class AkaitBuilderExtensions
{
    internal const string TokenClientName = "akait";

    public static IUmbracoBuilder AddAkaitMembershipResolver(
        this IUmbracoBuilder builder,
        Action<AkaitApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<AkaitApiOptions>()
            .Bind(builder.Config.GetSection(AkaitApiOptions.SectionName));

        if (configure is not null)
        {
            builder.Services.PostConfigure(configure);
        }

        // Token-endpoint values are read at startup; AccessTokenManagement caches the access token
        // in IDistributedCache (in-memory by default) and refreshes on expiry or 401.
        var snapshot = new AkaitApiOptions();
        builder.Config.GetSection(AkaitApiOptions.SectionName).Bind(snapshot);
        configure?.Invoke(snapshot);

        builder.Services.AddAccessTokenManagement(options =>
        {
            options.Client.Clients.Add(TokenClientName, new ClientCredentialsTokenRequest
            {
                Address = snapshot.TokenUrl,
                ClientId = snapshot.ClientId,
                ClientSecret = snapshot.ClientSecret,
                Scope = snapshot.Scope,
            });
        });

        builder.Services
            .AddHttpClient<IAkaitMembershipClient, AkaitMembershipClient>(
                (sp, http) =>
                {
                    var opts = sp.GetRequiredService<IOptionsMonitor<AkaitApiOptions>>().CurrentValue;
                    if (!string.IsNullOrEmpty(opts.BaseUrl))
                    {
                        http.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
                    }
                    http.Timeout = opts.Timeout;
                })
            .AddClientAccessTokenHandler(TokenClientName)
            .AddStandardResilienceHandler();

        builder.Services.AddScoped<IPostLoginHandler, AkaitMembershipResolver>();

        return builder;
    }
}
