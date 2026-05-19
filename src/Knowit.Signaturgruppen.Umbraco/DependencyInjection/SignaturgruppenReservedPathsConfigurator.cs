using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.Constants;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;

namespace Knowit.Signaturgruppen.Umbraco.DependencyInjection;

internal sealed class SignaturgruppenReservedPathsConfigurator : IPostConfigureOptions<GlobalSettings>
{
    private readonly IOptionsMonitor<SignaturgruppenOptions> _options;

    public SignaturgruppenReservedPathsConfigurator(IOptionsMonitor<SignaturgruppenOptions> options)
        => _options = options;

    public void PostConfigure(string? name, GlobalSettings options)
    {
        var sg = _options.CurrentValue;
        AddReservation(options, sg.CallbackPath);
        AddReservation(options, sg.SignedOutCallbackPath);
        AddReservation(options, SignaturgruppenDefaults.StepUpCallbackPath);
    }

    private static void AddReservation(GlobalSettings options, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var entry = "~" + (path.StartsWith('/') ? path : "/" + path);
        if (!entry.EndsWith('/'))
        {
            entry += "/";
        }

        var current = options.ReservedPaths ?? string.Empty;
        if (current.Contains(entry, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (current.Length > 0 && !current.EndsWith(','))
        {
            current += ",";
        }

        options.ReservedPaths = current + entry + ",";
    }
}
