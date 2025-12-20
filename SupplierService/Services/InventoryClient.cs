using System.Net.Http.Json;
using Shared;

namespace SupplierService.Services
{
    public class InventoryClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public InventoryClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<bool> ImportToInventory(int drugId, int quantity, DateTime? expiryDate = null)
        {
            var baseUrl = await ResolveInventoryBaseUrlAsync();
            var httpClient = _httpClientFactory.CreateClient();

            var payload = new
            {
                drugId = drugId,
                quantity = quantity,
                expiryDate = expiryDate
            };

            var response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/inventory/import", payload);
            return response.IsSuccessStatusCode;
        }

        private async Task<string> ResolveInventoryBaseUrlAsync()
        {
            var registryUrl = _configuration["ServiceRegistry:BaseUrl"] ?? "http://localhost:6000";
            var fallback = _configuration["InventoryService:BaseUrl"] ?? "http://localhost:5006";

            try
            {
                var discoveryClient = new ServiceDiscoveryClient(registryUrl);
                var service = await discoveryClient.FindServiceAsync("InventoryService");
                if (service != null && !string.IsNullOrWhiteSpace(service.Url))
                {
                    return service.Url.TrimEnd('/');
                }
            }
            catch
            {
                // ignore and fallback
            }

            return fallback.TrimEnd('/');
        }
    }
}
