using System.Threading.Tasks;

namespace Bee.Hub.AspNetCore
{
    public interface IHubSignalRBridge
    {
        Task BroadcastAsync(string group, string method, object? payload);
    }
}