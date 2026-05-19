using Microsoft.Extensions.Logging;

namespace Knowit.Signaturgruppen.Umbraco.Pipeline;

internal sealed class PostLoginPipeline
{
    private readonly IEnumerable<IPostLoginHandler> _handlers;
    private readonly ILogger<PostLoginPipeline> _logger;

    public PostLoginPipeline(
        IEnumerable<IPostLoginHandler> handlers,
        ILogger<PostLoginPipeline> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<PostLoginResult> RunAsync(PostLoginContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var handler in _handlers)
        {
            ct.ThrowIfCancellationRequested();
            var result = await handler.HandleAsync(context, ct);

            if (result.Kind != PostLoginResultKind.Continue)
            {
                _logger.LogDebug(
                    "Post-login handler {Handler} short-circuited with {Kind}",
                    handler.GetType().Name,
                    result.Kind);
                return result;
            }
        }

        return PostLoginResult.Continue;
    }
}
