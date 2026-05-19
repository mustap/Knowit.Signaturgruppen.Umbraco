using Knowit.Signaturgruppen.Umbraco.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Knowit.Signaturgruppen.Umbraco.StepUp.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route(SignaturgruppenDefaults.StepUpCallbackPath)]
public sealed class StepUpCallbackController : Controller
{
    private const string FallbackReturnUrl = "/";

    private readonly ILogger<StepUpCallbackController> _logger;

    public StepUpCallbackController(ILogger<StepUpCallbackController> logger)
        => _logger = logger;

    [HttpGet]
    public IActionResult Get([FromQuery(Name = SignaturgruppenStepUpService.ReturnUrlQueryKey)] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            _logger.LogWarning("Step-up callback received without a valid local return URL.");
            return Redirect(FallbackReturnUrl);
        }

        return Redirect(returnUrl);
    }
}
