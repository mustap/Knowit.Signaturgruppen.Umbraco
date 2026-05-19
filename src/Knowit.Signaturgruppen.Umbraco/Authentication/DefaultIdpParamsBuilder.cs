using System.Text;
using System.Text.Json;
using Knowit.Signaturgruppen.Umbraco.Configuration;
using Microsoft.AspNetCore.Http;

namespace Knowit.Signaturgruppen.Umbraco.Authentication;

public sealed class DefaultIdpParamsBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string Build(HttpContext httpContext, SignaturgruppenOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(options);

        var mitid = new Dictionary<string, object?>
        {
            ["loa_value"] = LoaToString(options.MitId.LevelOfAssurance)
        };

        if (!string.IsNullOrEmpty(options.MitId.ReferenceText))
        {
            mitid["reference_text"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(options.MitId.ReferenceText));
        }

        if (options.MitId.EnableAppSwitch)
        {
            mitid["enable_app_switch"] = true;
        }

        if (options.MitId.RequirePsd2)
        {
            mitid["require_psd2"] = true;
        }

        if (!string.IsNullOrEmpty(options.MitId.UuidHint))
        {
            mitid["uuid_hint"] = options.MitId.UuidHint;
        }

        if (!string.IsNullOrEmpty(options.MitId.CprHint))
        {
            mitid["cpr_hint"] = options.MitId.CprHint;
        }

        var payload = new Dictionary<string, object?> { ["mitid"] = mitid };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string LoaToString(NsisLoa loa) => loa switch
    {
        NsisLoa.Low => "low",
        NsisLoa.Substantial => "substantial",
        NsisLoa.High => "high",
        _ => "substantial"
    };
}
