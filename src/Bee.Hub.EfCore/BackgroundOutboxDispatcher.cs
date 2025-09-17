using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Bee.Hub.EfCore.Entities;
using Bee.Hub.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Bee.Hub.EfCore
{
    internal class BackgroundOutboxDispatcher : IHostedService, IDisposable
    {
        private readonly IServiceProvider _provider;
        private readonly ILogger<BackgroundOutboxDispatcher> _logger;
        private readonly EfCoreOptions _options;
        private readonly Stores.IOutboxStore _outboxStore;
        private Timer? _timer;

        public BackgroundOutboxDispatcher(IServiceProvider provider, ILogger<BackgroundOutboxDispatcher> logger, EfCoreOptions options, Stores.IOutboxStore outboxStore)
        {
            _provider = provider;
            _logger = logger;
            _options = options;
            _outboxStore = outboxStore;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(async _ => await DispatchOnce(cancellationToken), null, 0, _options.DispatchIntervalMs);
            return Task.CompletedTask;
        }

        private async Task DispatchOnce(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var transport = scope.ServiceProvider.GetService<ITransport>();
                if (transport == null)
                {
                    _logger.LogDebug("No ITransport registered, skipping outbox dispatch");
                    return;
                }

                var now = DateTime.UtcNow;
                var batch = await _outboxStore.GetPendingBatchAsync(_options.DispatchBatchSize, now, cancellationToken);

                var sentIds = new System.Collections.Generic.List<Guid>();
                var deadLetterIds = new System.Collections.Generic.List<Guid>();
                var deadLetterItems = new System.Collections.Generic.List<System.ValueTuple<Guid, string>>();
                var attemptIncrementIds = new System.Collections.Generic.List<Guid>();
                foreach (var msg in batch)
                {
                    try
                    {
                        // Basic publish via transport (payload is stored as byte[] in OutboxMessage)
                        await transport.PublishAsync(msg.MessageType, msg.Payload);
                        sentIds.Add(msg.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispatch outbox message {OutboxId}", msg.Id);
                        // update attempt count / dead letter via store
                        if (msg.AttemptCount + 1 >= _options.MaxRetryAttempts)
                        {
                            deadLetterIds.Add(msg.Id);
                            // prepare per-message deadletter metadata
                            var serializer = scope.ServiceProvider.GetService<IMessageSerializer>();
                            string json;
                            var info = new DeadLetterInfo
                            {
                                Reason = ex.Message,
                                ExceptionType = ex.GetType().FullName,
                                ExceptionMessage = ex.Message,
                                StackTrace = ex.StackTrace,
                                OccurredAt = DateTime.UtcNow
                            };
                            if (serializer != null)
                            {
                                var bytes = serializer.Serialize(info);
                                json = Convert.ToBase64String(bytes);
                            }
                            else
                            {
                                json = System.Text.Json.JsonSerializer.Serialize(info);
                            }

                            deadLetterItems.Add((msg.Id, json));
                        }
                        else
                        {
                            // collect for batch increment
                            attemptIncrementIds.Add(msg.Id);
                        }
                    }
                }

                // mark sent in batch
                if (sentIds.Any())
                {
                    await _outboxStore.MarkSentBatchAsync(sentIds, cancellationToken);
                }

                // batch increment attempts
                if (attemptIncrementIds.Any())
                {
                    await _outboxStore.IncrementAttemptBatchAsync(attemptIncrementIds, cancellationToken);
                }

                if (deadLetterIds.Any())
                {
                    // use a batch dead-letter marking to reduce DB writes
                    await _outboxStore.MarkDeadLetterBatchAsync(deadLetterIds, "MaxRetriesExceeded", cancellationToken);
                }
            
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch error");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}