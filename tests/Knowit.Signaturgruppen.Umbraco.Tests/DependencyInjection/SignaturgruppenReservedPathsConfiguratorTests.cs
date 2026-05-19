using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;

namespace Knowit.Signaturgruppen.Umbraco.Tests.DependencyInjection;

public class SignaturgruppenReservedPathsConfiguratorTests
{
    [Fact]
    public void Appends_callback_path_in_umbraco_format()
    {
        var sg = new SignaturgruppenOptions { CallbackPath = "/sg/login/callback" };
        var sut = new SignaturgruppenReservedPathsConfigurator(Wrap(sg));
        var settings = new GlobalSettings();

        sut.PostConfigure(null, settings);

        Assert.Contains("~/sg/login/callback/,", settings.ReservedPaths, StringComparison.Ordinal);
        Assert.EndsWith(",", settings.ReservedPaths);
    }

    [Fact]
    public void Reserves_callback_signedout_callback_and_stepup_callback()
    {
        var sg = new SignaturgruppenOptions
        {
            CallbackPath = "/sg/login/callback",
            SignedOutCallbackPath = "/sg/logout/callback"
        };
        var sut = new SignaturgruppenReservedPathsConfigurator(Wrap(sg));
        var settings = new GlobalSettings();

        sut.PostConfigure(null, settings);

        Assert.Contains("~/sg/login/callback/,", settings.ReservedPaths, StringComparison.Ordinal);
        Assert.Contains("~/sg/logout/callback/,", settings.ReservedPaths, StringComparison.Ordinal);
        Assert.Contains("~/sg/step-up/callback/,", settings.ReservedPaths, StringComparison.Ordinal);
    }

    [Fact]
    public void Idempotent_does_not_duplicate_entry()
    {
        var sg = new SignaturgruppenOptions { CallbackPath = "/sg/login/callback" };
        var sut = new SignaturgruppenReservedPathsConfigurator(Wrap(sg));
        var settings = new GlobalSettings();

        sut.PostConfigure(null, settings);
        var first = settings.ReservedPaths;
        sut.PostConfigure(null, settings);

        Assert.Equal(first, settings.ReservedPaths);
    }

    private static IOptionsMonitor<SignaturgruppenOptions> Wrap(SignaturgruppenOptions options)
        => new StaticOptionsMonitor(options);

    private sealed class StaticOptionsMonitor : IOptionsMonitor<SignaturgruppenOptions>
    {
        public StaticOptionsMonitor(SignaturgruppenOptions value) => CurrentValue = value;

        public SignaturgruppenOptions CurrentValue { get; }

        public SignaturgruppenOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SignaturgruppenOptions, string?> listener) => null;
    }
}
