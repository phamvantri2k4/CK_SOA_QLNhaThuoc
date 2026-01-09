using System.Net.Http.Json;
using Shared.Services;

namespace SupplierService.Services
{
    public class InventoryClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConsulServiceDiscovery _consulDiscovery;

        public InventoryClient(
            IHttpClientFactory httpClientFactory,
            ConsulServiceDiscovery consulDiscovery)
        {
            _httpClientFactory = httpClientFactory;
            _consulDiscovery = consulDiscovery;
        }

        public async Task<bool> ImportToInventory(int drugId, int quantity, DateTime? expiryDate = null, string unitType = "box")
        {
            var baseUrl = await _consulDiscovery.GetServiceUrlAsync("InventoryService");
            var httpClient = _httpClientFactory.CreateClient();

            var payload = new
            {
                drugId = drugId,
                quantity = quantity,
                unitType = unitType,
                expiryDate = expiryDate
            };

            var response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/inventory/import", payload);
            return response.IsSuccessStatusCode;
        }
    }
}
