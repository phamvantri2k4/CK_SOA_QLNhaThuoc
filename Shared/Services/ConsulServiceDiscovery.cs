using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Services
{
    /// <summary>
    /// Service Discovery helper để tìm địa chỉ của các microservices qua Consul.
    /// SHARED cho tất cả services - giảm duplicate code.
    /// </summary>
    public class ConsulServiceDiscovery
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConsulServiceDiscovery> _logger;

        public ConsulServiceDiscovery(
            IConfiguration configuration,
            ILogger<ConsulServiceDiscovery> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Lấy URL của service qua Consul.
        /// PURE SERVICE DISCOVERY - Không có fallback URLs!
        /// Nếu Consul không tìm thấy service → throw exception.
        /// </summary>
        public async Task<string> GetServiceUrlAsync(string serviceName)
        {
            try
            {
                // Query Consul
                using var consulClient = new ConsulClient(config =>
                {
                    config.Address = new Uri("http://localhost:8500");
                });

                var services = await consulClient.Health.Service(serviceName, null, true);
                var service = services.Response?.FirstOrDefault();

                if (service != null)
                {
                    var url = $"http://{service.Service.Address}:{service.Service.Port}";
                    _logger.LogInformation($"✅ Discovered {serviceName} at {url} via Consul");
                    return url;
                }

                // Service not found in Consul
                var errorMsg = $"Service '{serviceName}' not found in Consul. Make sure the service is running and registered.";
                _logger.LogError($"❌ {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // Consul connection error
                var errorMsg = $"Cannot connect to Consul for service '{serviceName}': {ex.Message}. Is Consul running on http://localhost:8500?";
                _logger.LogError($"❌ {errorMsg}");
                throw new InvalidOperationException(errorMsg, ex);
            }
        }

        /// <summary>
        /// Lấy danh sách TẤT CẢ services đăng ký trong Consul
        /// Dùng cho dashboard/monitoring
        /// </summary>
        public async Task<List<Consul.AgentService>> GetAllServicesAsync()
        {
            try
            {
                using var consulClient = new ConsulClient(config =>
                {
                    config.Address = new Uri("http://localhost:8500");
                });

                var services = await consulClient.Agent.Services();
                _logger.LogInformation($"✅ Retrieved {services.Response.Count} services from Consul");
                return services.Response.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error fetching all services from Consul: {ex.Message}");
                return new List<Consul.AgentService>();
            }
        }
    }
}
