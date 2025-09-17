using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bee.Hub.AspNetCore
{
    public class BeeHubMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<BeeHubMiddleware> _logger;

        public BeeHubMiddleware(RequestDelegate next, ILogger<BeeHubMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();

            if (!context.Request.Headers.ContainsKey("bh-correlation-id"))
            {
                var cid = System.Guid.NewGuid().ToString();
                context.Request.Headers["bh-correlation-id"] = cid;
            }

            _logger.LogInformation("[Bee.Hub] Request start {Method} {Path} CorrelationId={CorrelationId}", context.Request.Method, context.Request.Path, context.Request.Headers["bh-correlation-id"].ToString());

            await _next(context).ConfigureAwait(false);

            sw.Stop();
            _logger.LogInformation("[Bee.Hub] Request finished {Method} {Path} DurationMs={Duration} CorrelationId={CorrelationId}", context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds, context.Request.Headers["bh-correlation-id"].ToString());
        }
    }
}