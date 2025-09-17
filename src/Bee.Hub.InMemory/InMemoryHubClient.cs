using System;
using System.Threading.Tasks;
using Bee.Hub.Core;

namespace Bee.Hub.InMemory
{
    public class InMemoryHubClient : IHubClient, IAsyncDisposable
    {
        private readonly ITransport _transport;
        private readonly IMessageSerializer _serializer;

        public InMemoryHubClient(ITransport transport, IMessageSerializer serializer)
        {
            _transport = transport;
            _serializer = serializer;
        }

        public Task PublishAsync<T>(T message, object? headers = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var topic = typeof(T).FullName ?? typeof(T).Name;
            var data = _serializer.Serialize(message!);
            return _transport.PublishAsync(topic, data, null);
        }

        public Task SendAsync<T>(string destination, T message, object? headers = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var data = _serializer.Serialize(message!);
            return _transport.PublishAsync(destination, data, null);
        }

        public ValueTask DisposeAsync()
        {
            return _transport.DisposeAsync();
        }

        public Task StartAsync() => _transport.StartAsync();
        public Task StopAsync() => _transport.StopAsync();
    }
}