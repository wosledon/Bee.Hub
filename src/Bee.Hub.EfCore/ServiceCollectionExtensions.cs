using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bee.Hub.EfCore
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBeeHubEfCore(this IServiceCollection services, Action<EfCoreOptions>? configure = null)
        {
            var options = new EfCoreOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            // register the SaveChanges interceptor so applications can add it to their DbContext options
            services.AddScoped<OutboxSaveChangesInterceptor>();

            // register default store implementations (scoped)
            services.AddScoped<Stores.IOutboxStore, Stores.EfCoreOutboxStore>();
            services.AddScoped<Stores.IInboxStore, Stores.EfCoreInboxStore>();

            // DbContext should be configured by the application (AddDbContext<BeeHubDbContext>).
            // Example registration in the host application:
            // services.AddDbContext<BeeHubDbContext>((provider, options) =>
            // {
            //     options.UseNpgsql(configuration.GetConnectionString("BeeHub"));
            //     // add registered interceptors from DI
            //     var interceptor = provider.GetRequiredService<OutboxSaveChangesInterceptor>();
            //     options.AddInterceptors(interceptor);
            // });

            services.AddHostedService<BackgroundOutboxDispatcher>();

            return services;
        }
    }
}