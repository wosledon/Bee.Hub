using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bee.Hub.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bee.Hub.RabbitMQ
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public string Exchange { get; set; } = "bee.hub.exchange";
        public string DeadLetterExchange { get; set; } = "bee.hub.dlx";
        public Bee.Hub.Core.RetryOptions Retry { get; set; } = new Bee.Hub.Core.RetryOptions();
    }

    public class RabbitMqTransport : ITransport
    {
        private readonly RabbitMqOptions _options;
        private IConnection? _connection;
        private IModel? _channel;
        private CancellationTokenSource? _cts;

        public RabbitMqTransport(RabbitMqOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task StartAsync()
        {
            var factory = new ConnectionFactory() { HostName = _options.HostName };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
            _channel.ExchangeDeclare(_options.DeadLetterExchange, ExchangeType.Fanout, durable: true);

            _cts = new CancellationTokenSource();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
                _channel?.Close();
                _connection?.Close();
            }
            catch { }
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
                _cts?.Dispose();
            }
            catch { }
            return ValueTask.CompletedTask;
        }

        public Task PublishAsync(string topic, byte[] payload, IDictionary<string, string>? headers = null)
        {
            if (_channel == null) throw new InvalidOperationException("Channel not started");

            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            if (headers != null)
            {
                props.Headers = new Dictionary<string, object>();
                foreach (var h in headers)
                {
                    props.Headers[h.Key] = Encoding.UTF8.GetBytes(h.Value);
                }
            }

            _channel.BasicPublish(exchange: _options.Exchange, routingKey: topic, basicProperties: props, body: payload);
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topic, Func<byte[], IDictionary<string, string>?, Task> handler)
        {
            if (_channel == null) throw new InvalidOperationException("Channel not started");

            var queueName = _channel.QueueDeclare().QueueName;
            _channel.QueueBind(queue: queueName, exchange: _options.Exchange, routingKey: topic);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                IDictionary<string, string>? hdrs = null;
                if (ea.BasicProperties?.Headers != null)
                {
                    hdrs = new Dictionary<string, string>();
                    foreach (var kv in ea.BasicProperties.Headers)
                    {
                        if (kv.Value is byte[] b)
                        {
                            hdrs[kv.Key] = Encoding.UTF8.GetString(b);
                        }
                        else if (kv.Value != null)
                        {
                            hdrs[kv.Key] = kv.Value.ToString()!;
                        }
                    }
                }

                var attempt = 0;
                var delay = _options.Retry.InitialDelayMs;
                var handled = false;
                while (attempt <= _options.Retry.MaxRetries && !handled)
                {
                    try
                    {
                        await handler(ea.Body.ToArray(), hdrs).ConfigureAwait(false);
                        _channel.BasicAck(ea.DeliveryTag, multiple: false);
                        handled = true;
                    }
                    catch (Exception)
                    {
                        attempt++;
                        if (attempt > _options.Retry.MaxRetries)
                        {
                            var props = _channel.CreateBasicProperties();
                            props.Persistent = true;
                            _channel.BasicPublish(exchange: _options.DeadLetterExchange, routingKey: "", basicProperties: props, body: ea.Body);
                            _channel.BasicAck(ea.DeliveryTag, multiple: false);
                            break;
                        }

                        try
                        {
                            await Task.Delay(delay).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { break; }

                        delay = (int)(delay * _options.Retry.BackoffFactor);
                    }
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }
    }
}