using System.Security.Claims;
using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Knowit.Signaturgruppen.Umbraco.Members;

internal sealed class MemberAutoLinkConfigurator
{
    internal const string SyntheticEmailDomain = "signaturgruppen.local";

    private readonly IOptionsMonitor<SignaturgruppenOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberAutoLinkConfigurator> _logger;

    public MemberAutoLinkConfigurator(
        IOptionsMonitor<SignaturgruppenOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<MemberAutoLinkConfigurator> logger)
    {
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void OnAutoLinking(MemberIdentityUser user, ExternalLoginInfo info)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(info);

        var displayName = info.Principal.FindFirstValue(ClaimMaps.MitIdIdentityName)
                          ?? info.Principal.FindFirstValue(ClaimTypes.Name)
                          ?? info.Principal.Identity?.Name
                          ?? $"sg:{info.ProviderKey}";
        user.Name = displayName;

        var sub = info.Principal.FindFirstValue(ClaimMaps.Sub) ?? info.ProviderKey;
        var email = $"{sub}@{SyntheticEmailDomain}";
        user.Email = email;
        user.UserName = email;
    }

    public bool OnExternalLogin(MemberIdentityUser user, ExternalLoginInfo info)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(info);

        if (user.Key == Guid.Empty)
        {
            return true;
        }

        var claimToProperty = _options.CurrentValue.ClaimToMemberProperty;
        if (claimToProperty.Count == 0)
        {
            return true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var memberService = scope.ServiceProvider.GetRequiredService<IMemberService>();
            var member = memberService.GetByKey(user.Key);
            if (member is null)
            {
                return true;
            }

            var changed = false;
            foreach (var pair in claimToProperty)
            {
                if (!member.Properties.Contains(pair.Value))
                {
                    continue;
                }

                var value = info.Principal.FindFirstValue(pair.Key);
                if (value is null)
                {
                    continue;
                }

                var current = member.GetValue<string?>(pair.Value);
                if (!string.Equals(current, value, StringComparison.Ordinal))
                {
                    member.SetValue(pair.Value, value);
                    changed = true;
                }
            }

            if (changed)
            {
                memberService.Save(member);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply external claim mappings to member {MemberKey}", user.Key);
        }

        return true;
    }
}
