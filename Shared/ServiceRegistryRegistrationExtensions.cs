using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shared
{
    public static class ServiceRegistryRegistrationExtensions
    {
        public static void RegisterWithServiceRegistryOnStart(
            this IHostApplicationLifetime lifetime,
            IServiceProvider services,
            IConfiguration configuration,
            string serviceName,
            string description,
            string version,
            string defaultPort,
            int defaultHeartbeatSeconds = 20,
            int startupDelayMs = 2000)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(startupDelayMs);

                    var registryUrl = configuration["ServiceRegistry:BaseUrl"] ?? "http://localhost:6000";
                    var discoveryClient = new ServiceDiscoveryClient(registryUrl);

                    var server = services.GetRequiredService<IServer>();
                    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;

                    var url = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                              ?? addresses?.FirstOrDefault()
                              ?? (configuration["ServiceInfo:Port"] is string p ? $"http://localhost:{p}" : $"http://localhost:{defaultPort}");

                    url = url.TrimEnd('/');

                    var serviceInfo = new ServiceInfo
                    {
                        ServiceName = serviceName,
                        Url = url,
                        Description = description,
                        Version = version
                    };

                    var ok = await discoveryClient.RegisterServiceAsync(serviceInfo);
                    if (!ok) return;

                    lifetime.ApplicationStopping.Register(() =>
                    {
                        _ = Task.Run(async () =>
                        {
                            await discoveryClient.UnregisterServiceAsync(serviceName);
                        });
                    });

                    _ = Task.Run(async () =>
                    {
                        var intervalSeconds = configuration.GetValue<int?>("ServiceInfo:HeartbeatSeconds") ?? defaultHeartbeatSeconds;

                        while (!lifetime.ApplicationStopping.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), lifetime.ApplicationStopping);
                                await discoveryClient.SendHeartbeatAsync(serviceName);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch
                            {
                            }
                        }
                    });
                });
            });
        }
    }
}
