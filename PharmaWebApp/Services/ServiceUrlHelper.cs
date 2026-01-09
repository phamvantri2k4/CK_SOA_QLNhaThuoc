using Shared.Services;

namespace PharmaWebApp.Services
{
    /// <summary>
    /// Helper đơn giản để lấy service URLs từ Consul
    /// </summary>
    public class ServiceUrlHelper
    {
        private readonly ConsulServiceDiscovery _consul;
        private readonly IConfiguration _config;

        public ServiceUrlHelper(ConsulServiceDiscovery consul, IConfiguration config)
        {
            _consul = consul;
            _config = config;
        }

        public async Task<string> GetDrugServiceUrlAsync()
        {
            return await _consul.GetServiceUrlAsync("DrugService");
        }

        public async Task<string> GetAuthServiceUrlAsync()
        {
            return await _consul.GetServiceUrlAsync("AuthService");
        }

        public async Task<string> GetSaleServiceUrlAsync()
        {
            return await _consul.GetServiceUrlAsync("SaleService");
        }

        public async Task<string> GetInventoryServiceUrlAsync()
        {
            return await _consul.GetServiceUrlAsync("InventoryService");
        }

        public async Task<string> GetCustomerServiceUrlAsync()
        {
            return await _consul.GetServiceUrlAsync("CustomerService");
        }

        public async Task<string> GetSupplierServiceUrlAsync()
        {
            return await _consul.GetServiceUrlAsync("SupplierService");
        }

        public async Task<string> GetReportingServiceUrlAsync()
        {
            return await _consul.GetServiceUrlAsync("ReportingService");
        }

        // Generic method cho bất kỳ service nào
        public async Task<string> GetServiceUrlAsync(string serviceName)
        {
            return await _consul.GetServiceUrlAsync(serviceName);
        }
    }
}
