using Consul;

namespace PharmaWebApp.Services
{
    public class ConsulServiceDiscovery
    {
        private readonly IConsulClient _consulClient;
        private readonly IConfiguration _configuration;

        public ConsulServiceDiscovery(IConfiguration configuration)
        {
            _configuration = configuration;
            _consulClient = new ConsulClient(c => c.Address = new Uri("http://localhost:8500"));
        }

        public async Task<string> GetServiceUrlAsync(string serviceName)
        {
            try
            {
                // Query Consul để tìm service
                var services = await _consulClient.Health.Service(serviceName, tag: null, passingOnly: true);

                if (services.Response != null && services.Response.Any())
                {
                    var service = services.Response.First().Service;
                    var url = $"http://{service.Address}:{service.Port}";
                    Console.WriteLine($"✅ Tìm thấy {serviceName} qua Consul: {url}");
                    return url;
                }

                Console.WriteLine($"⚠️ Không tìm thấy {serviceName} trong Consul, dùng fallback");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi query Consul cho {serviceName}: {ex.Message}");
            }

            // Fallback về appsettings.json nếu Consul fail
            var fallbackUrl = _configuration[$"{serviceName}:BaseUrl"];
            return fallbackUrl ?? throw new Exception($"Không tìm thấy {serviceName}");
        }

        public async Task<List<AgentService>> GetAllServicesAsync()
        {
            try
            {
                var services = await _consulClient.Agent.Services();
                return services.Response.Values.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching all services: {ex.Message}");
                return new List<AgentService>();
            }
        }
    }
}
