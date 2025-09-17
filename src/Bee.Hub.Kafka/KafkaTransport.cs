using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Bee.Hub.Core;

namespace Bee.Hub.Kafka
{
    public class KafkaTransport : ITransport
    {
        private readonly KafkaOptions _options;
        private IProducer<byte[], byte[]>? _producer;
        private IConsumer<byte[], byte[]>? _consumer;
        private CancellationTokenSource? _cts;

        public KafkaTransport(KafkaOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task StartAsync()
        {
            _producer = new ProducerBuilder<byte[], byte[]>(_options.ProducerConfig).Build();
            _consumer = new ConsumerBuilder<byte[], byte[]>(_options.ConsumerConfig).Build();
            _cts = new CancellationTokenSource();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
                _consumer?.Close();
            }
            catch { }
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                _producer?.Dispose();
                _consumer?.Dispose();
                _cts?.Dispose();
            }
            catch { }
            return ValueTask.CompletedTask;
        }

        public async Task PublishAsync(string topic, byte[] payload, IDictionary<string, string>? headers = null)
        {
            if (_producer == null) throw new InvalidOperationException("Producer not started");

            var msg = new Message<byte[], byte[]>
            {
                Value = payload,
                Headers = new Headers()
            };

            if (headers != null)
            {
                foreach (var h in headers)
                {
                    msg.Headers.Add(h.Key, System.Text.Encoding.UTF8.GetBytes(h.Value));
                }
            }

            var dr = await _producer.ProduceAsync(topic, msg).ConfigureAwait(false);
        }

        public Task SubscribeAsync(string topic, Func<byte[], IDictionary<string, string>?, Task> handler)
        {
            if (_consumer == null) throw new InvalidOperationException("Consumer not started");

            _consumer.Subscribe(topic);
            _ = Task.Run(async () =>
            {
                while (!_cts!.IsCancellationRequested)
                {
                    try
                    {
                        var cr = _consumer.Consume(_cts.Token);
                        if (cr == null) continue;

                        var payload = cr.Message.Value;
                        IDictionary<string, string>? hdrs = null;
                        if (cr.Message.Headers != null && cr.Message.Headers.Count > 0)
                        {
                            hdrs = cr.Message.Headers.ToDictionary(h => h.Key, h => System.Text.Encoding.UTF8.GetString(h.GetValueBytes()));
                        }

                        var attempt = 0;
                        var success = false;
                        var delay = _options.Retry.InitialDelayMs;
                        while (attempt <= _options.Retry.MaxRetries && !success)
                        {
                            try
                            {
                                await handler(payload, hdrs).ConfigureAwait(false);
                                success = true;
                            }
                            catch (Exception)
                            {
                                attempt++;
                                if (attempt > _options.Retry.MaxRetries)
                                {
                                    if (!string.IsNullOrEmpty(_options.DeadLetterTopic))
                                    {
                                        var msg = new Message<byte[], byte[]>
                                        {
                                            Value = payload,
                                            Headers = new Headers()
                                        };
                                        if (hdrs != null)
                                        {
                                            foreach (var h in hdrs) msg.Headers.Add(h.Key, System.Text.Encoding.UTF8.GetBytes(h.Value));
                                        }
                                        await _producer!.ProduceAsync(_options.DeadLetterTopic, msg).ConfigureAwait(false);
                                    }
                                    break;
                                }

                                try
                                {
                                    await Task.Delay(delay, _cts!.Token).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException) { break; }

                                delay = (int)(delay * _options.Retry.BackoffFactor);
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch { /* swallow to keep loop alive */ }
                }
            });

            return Task.CompletedTask;
        }
    }
}