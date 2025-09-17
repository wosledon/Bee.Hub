using System.Threading.Tasks;
using System.Threading;

namespace Bee.Hub.Core
{
    public interface IHubClient
    {
        Task PublishAsync<T>(T message, object? headers = null, CancellationToken cancellationToken = default);
        Task SendAsync<T>(string destination, T message, object? headers = null, CancellationToken cancellationToken = default);

        // lifecycle
        Task StartAsync();
        Task StopAsync();
    }
}