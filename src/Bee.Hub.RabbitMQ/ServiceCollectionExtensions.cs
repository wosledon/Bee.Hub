using Microsoft.Extensions.DependencyInjection;
using System;
using Bee.Hub.Core;

namespace Bee.Hub.RabbitMQ
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBeeHubRabbitMq(this IServiceCollection services, Action<RabbitMqOptions>? configure = null)
        {
            var opts = new RabbitMqOptions();
            configure?.Invoke(opts);
            services.AddSingleton(opts);
            services.AddSingleton<ITransport, RabbitMqTransport>(sp => new RabbitMqTransport(sp.GetRequiredService<RabbitMqOptions>()));
            return services;
        }
    }
}