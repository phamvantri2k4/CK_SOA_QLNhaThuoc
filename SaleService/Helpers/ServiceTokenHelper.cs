using System.Net.Http.Json;
using System.Text.Json;

namespace SaleService.Helpers
{
    /// <summary>
    /// Helper để lấy service token từ AuthService
    /// </summary>
    public class ServiceTokenHelper
    {
        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        private const string ServiceKey = "ServiceKey123!";

        public static async Task<string?> GetServiceTokenAsync(HttpClient httpClient, string authServiceUrl)
        {
            // Nếu token còn hiệu lực, dùng lại
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            try
            {
                var request = new { ServiceKey = ServiceKey };
                var response = await httpClient.PostAsJsonAsync($"{authServiceUrl}/api/auth/service-token", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    _cachedToken = result.GetProperty("token").GetString();
                    _tokenExpiry = DateTime.UtcNow.AddHours(23); // Token hết hạn sau 23 giờ
                    return _cachedToken;
                }
            }
            catch
            {
                // Log error nếu cần
            }

            return null;
        }
    }
}

