using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Bee.Hub.Core;
using System;

namespace Bee.Hub.AspNetCore
{
    internal class BeeHubHostedService : IHostedService
    {
        private readonly IHubClient _client;
        private readonly ILogger<BeeHubHostedService> _logger;

        public BeeHubHostedService(IHubClient client, ILogger<BeeHubHostedService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Bee.Hub HostedService");
            try
            {
                await _client.StartAsync().ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error starting IHubClient");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Bee.Hub HostedService");
            try
            {
                await _client.StopAsync().ConfigureAwait(false);

                if (_client is IAsyncDisposable ad)
                {
                    await ad.DisposeAsync();
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error stopping IHubClient");
            }
        }
    }
}