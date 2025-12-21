using System.Net.Http.Json;
using System.Text.Json;

namespace Shared.Helpers
{
    /// <summary>
    /// Lấy và cache Service Token từ AuthService
    /// Dùng cho service-to-service communication
    /// </summary>
    public static class ServiceTokenHelper
    {
        private static string? _token;
        private static DateTime _expireAt = DateTime.MinValue;

        // ⚠️ Nên đưa ServiceKey vào appsettings.json (ở đây để đơn giản cho đồ án)
        private const string ServiceKey = "ServiceKey123!";

        public static async Task<string?> GetServiceTokenAsync(
            HttpClient httpClient,
            string authServiceBaseUrl)
        {
            // Nếu token còn hạn → dùng lại
            if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _expireAt)
            {
                return _token;
            }

            try
            {
                var response = await httpClient.PostAsJsonAsync(
                    $"{authServiceBaseUrl.TrimEnd('/')}/api/auth/service-token",
                    new { ServiceKey });

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                _token = json.GetProperty("token").GetString();

                // Set hạn dùng an toàn (ngắn hơn JWT thực tế)
                _expireAt = DateTime.UtcNow.AddHours(22);

                return _token;
            }
            catch
            {
                return null;
            }
        }
    }
}
