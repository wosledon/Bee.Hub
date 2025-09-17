using Microsoft.Extensions.DependencyInjection;
using Bee.Hub.Core;

namespace Bee.Hub.InMemory
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBeeHubInMemory(this IServiceCollection services)
        {
            services.AddSingleton<IMessageSerializer, DefaultJsonSerializer>();
            services.AddSingleton<ITransport, InMemoryTransport>();
            services.AddSingleton<InMemoryHubClient>(sp => new InMemoryHubClient(sp.GetRequiredService<ITransport>(), sp.GetRequiredService<IMessageSerializer>()));
            services.AddSingleton<IHubClient>(sp => sp.GetRequiredService<InMemoryHubClient>());
            return services;
        }
    }
}
