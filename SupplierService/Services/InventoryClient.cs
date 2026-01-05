using System.Net.Http.Json;
using Consul;

namespace SupplierService.Services
{
    public class InventoryClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConsulClient _consulClient;

        public InventoryClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _consulClient = new ConsulClient(c => c.Address = new Uri("http://localhost:8500"));
        }

        public async Task<bool> ImportToInventory(int drugId, int quantity, DateTime? expiryDate = null, string unitType = "box")
        {
            var baseUrl = await GetServiceUrlAsync("InventoryService");
            var httpClient = _httpClientFactory.CreateClient();

            var payload = new
            {
                drugId = drugId,
                quantity = quantity,
                unitType = unitType, // Gửi thêm unitType
                expiryDate = expiryDate
            };

            var response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/inventory/import", payload);
            return response.IsSuccessStatusCode;
        }

        private async Task<string> GetServiceUrlAsync(string serviceName)
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

                Console.WriteLine($"⚠️ Không tìm thấy {serviceName} trong Consul");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi query Consul: {ex.Message}");
            }

            // Fallback về hardcode nếu Consul fail
            return "http://localhost:5006";
        }
    }
}
