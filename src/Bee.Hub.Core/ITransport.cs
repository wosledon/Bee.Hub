using System.Threading.Tasks;
using System;

namespace Bee.Hub.Core
{
    public interface ITransport : IAsyncDisposable
    {
        Task StartAsync();
        Task StopAsync();

        Task PublishAsync(string topic, byte[] payload, System.Collections.Generic.IDictionary<string,string>? headers = null);
        Task SubscribeAsync(string topic, Func<byte[], System.Collections.Generic.IDictionary<string,string>?, Task> handler);
    }
}