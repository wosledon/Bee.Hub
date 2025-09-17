using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Bee.Hub.AspNetCore
{
    internal class DefaultHubSignalRBridge : IHubSignalRBridge
    {
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<BeeHubSignalRHub> _hubContext;

        public DefaultHubSignalRBridge(Microsoft.AspNetCore.SignalR.IHubContext<BeeHubSignalRHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task BroadcastAsync(string group, string method, object? payload)
        {
            if (string.IsNullOrEmpty(group))
            {
                return _hubContext.Clients.All.SendAsync(method, payload ?? new { });
            }

            return _hubContext.Clients.Group(group).SendAsync(method, payload ?? new { });
        }
    }
}