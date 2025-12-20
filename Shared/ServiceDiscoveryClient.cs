using System.Net.Http.Json;

namespace Shared
{
    /// <summary>
    /// Client để tương tác với Service Registry
    /// Cung cấp các method để đăng ký service và tìm kiếm service
    /// </summary>
    public class ServiceDiscoveryClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _registryBaseUrl;

        public ServiceDiscoveryClient(string registryBaseUrl, HttpClient? httpClient = null)
        {
            _registryBaseUrl = registryBaseUrl;
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.BaseAddress = new Uri(registryBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Đăng ký service vào Service Registry (Publish)
        /// </summary>
        /// <param name="serviceInfo">Thông tin service cần đăng ký</param>
        /// <returns>True nếu đăng ký thành công, False nếu thất bại</returns>
        public async Task<bool> RegisterServiceAsync(ServiceInfo serviceInfo)
        {
            try
            {
                var endpoint = "/api/registry/register";
                var fullUrl = $"{_registryBaseUrl}{endpoint}";
                
                Console.WriteLine($"[ServiceDiscovery] Đang gửi request đến: {fullUrl}");
                Console.WriteLine($"[ServiceDiscovery] Service: {serviceInfo.ServiceName}, URL: {serviceInfo.Url}");
                
                var response = await _httpClient.PostAsJsonAsync(endpoint, serviceInfo);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ServiceDiscovery] Đăng ký service '{serviceInfo.ServiceName}' thành công tại {serviceInfo.Url}");
                    Console.WriteLine($"[ServiceDiscovery] Response: {responseContent}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ServiceDiscovery] Đăng ký service thất bại. Status: {response.StatusCode}");
                    Console.WriteLine($"[ServiceDiscovery] Error content: {errorContent}");
                    return false;
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                Console.WriteLine($"[ServiceDiscovery] Lỗi kết nối HTTP khi đăng ký service: {httpEx.Message}");
                if (httpEx.InnerException != null)
                {
                    Console.WriteLine($"[ServiceDiscovery] Inner exception: {httpEx.InnerException.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceDiscovery] Lỗi khi đăng ký service: {ex.Message}");
                Console.WriteLine($"[ServiceDiscovery] Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ServiceDiscovery] Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> UnregisterServiceAsync(string serviceName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/registry/services/{serviceName}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/registry/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tìm kiếm service theo tên (Find)
        /// </summary>
        /// <param name="serviceName">Tên service cần tìm</param>
        /// <returns>ServiceInfo nếu tìm thấy, null nếu không tìm thấy</returns>
        public async Task<ServiceInfo?> FindServiceAsync(string serviceName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/registry/services/{serviceName}");
                
                if (response.IsSuccessStatusCode)
                {
                    var serviceInfo = await response.Content.ReadFromJsonAsync<ServiceInfo>();
                    Console.WriteLine($"[ServiceDiscovery] Tìm thấy service '{serviceName}' tại {serviceInfo?.Url}");
                    return serviceInfo;
                }
                else
                {
                    Console.WriteLine($"[ServiceDiscovery] Không tìm thấy service '{serviceName}'. Status: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceDiscovery] Lỗi khi tìm service: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả các service đã đăng ký
        /// </summary>
        /// <returns>Danh sách ServiceInfo</returns>
        public async Task<List<ServiceInfo>> GetAllServicesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/registry/services");
                
                if (response.IsSuccessStatusCode)
                {
                    var services = await response.Content.ReadFromJsonAsync<List<ServiceInfo>>();
                    return services ?? new List<ServiceInfo>();
                }
                else
                {
                    Console.WriteLine($"[ServiceDiscovery] Không thể lấy danh sách service. Status: {response.StatusCode}");
                    return new List<ServiceInfo>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceDiscovery] Lỗi khi lấy danh sách service: {ex.Message}");
                return new List<ServiceInfo>();
            }
        }

        public async Task<bool> SendHeartbeatAsync(string serviceName)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/registry/heartbeat/{serviceName}", content: null);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                Console.WriteLine($"[ServiceDiscovery] Heartbeat thất bại cho '{serviceName}'. Status: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceDiscovery] Lỗi khi gửi heartbeat: {ex.Message}");
                return false;
            }
        }
    }
}

