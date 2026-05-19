using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Knowit.Signaturgruppen.Umbraco.Pipeline;

public sealed class PostLoginContext
{
    public PostLoginContext(HttpContext httpContext, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
        Properties = properties;
    }

    public HttpContext HttpContext { get; }

    public ClaimsPrincipal Principal { get; }

    public AuthenticationProperties? Properties { get; }

    public IServiceProvider RequestServices => HttpContext.RequestServices;
}
