using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Bee.Hub.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        // AspNetCore package provides host integration, middleware and SignalR bridge.
        public static IServiceCollection AddBeeHub(this IServiceCollection services, Action<AspNetCoreOptions>? configure = null)
        {
            var opts = new AspNetCoreOptions();
            configure?.Invoke(opts);
            services.AddSingleton(opts);
            services.AddHostedService<BeeHubHostedService>();
            if (opts.EnableSignalRBridge)
            {
                // register SignalR services and bridge
                services.AddSignalR();
                services.AddSingleton<IHubSignalRBridge, DefaultHubSignalRBridge>();
            }
            return services;
        }

        public static IApplicationBuilder UseBeeHub(this IApplicationBuilder app)
        {
            app.UseMiddleware<BeeHubMiddleware>();
            return app;
        }
    }
}
