using System.Net.Http.Json;
using System.Text.Json;

namespace SaleService.Helpers
{
    /// <summary>
    /// Lấy JWT token cho service-to-service (SOA)
    /// </summary>
    public static class ServiceTokenHelper
    {
        private static string? _token;
        private static DateTime _expireAt;

        // Dùng chung cho các service (đồ án cho phép hard-code)
        private const string ServiceKey = "ServiceKey123!";

        public static async Task<string?> GetServiceTokenAsync(
            HttpClient client,
            string authServiceUrl)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(authServiceUrl))
                return null;

            // Token còn hạn → dùng lại
            if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _expireAt)
                return _token;

            try
            {
                var response = await client.PostAsJsonAsync(
                    $"{authServiceUrl.TrimEnd('/')}/api/auth/service-token",
                    new { ServiceKey });

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                _token = json.GetProperty("token").GetString();
                _expireAt = DateTime.UtcNow.AddHours(23);

                return _token;
            }
            catch
            {
                return null;
            }
        }
    }
}
