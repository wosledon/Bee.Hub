using Microsoft.Extensions.DependencyInjection;
using System;
using Bee.Hub.Core;

namespace Bee.Hub.Kafka
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBeeHubKafka(this IServiceCollection services, Action<KafkaOptions>? configure = null)
        {
            var opts = new KafkaOptions();
            configure?.Invoke(opts);
            services.AddSingleton(opts);
            services.AddSingleton<ITransport, KafkaTransport>(sp => new KafkaTransport(sp.GetRequiredService<KafkaOptions>()));
            return services;
        }
    }
}