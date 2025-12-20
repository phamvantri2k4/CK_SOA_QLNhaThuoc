using Shared;

namespace PharmaWebApp.Services
{
    public class ServiceResolver : IServiceResolver
    {
        private readonly ServiceDiscoveryClient _discoveryClient;

        public ServiceResolver(ServiceDiscoveryClient discoveryClient)
        {
            _discoveryClient = discoveryClient;
        }

        public async Task<string?> GetOptionalAsync(string serviceName)
        {
            try
            {
                var info = await _discoveryClient.FindServiceAsync(serviceName);
                if (info == null || string.IsNullOrWhiteSpace(info.Url)) return null;
                return info.Url.TrimEnd('/');
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetRequiredAsync(string serviceName)
        {
            var url = await GetOptionalAsync(serviceName);
            if (!string.IsNullOrWhiteSpace(url)) return url;

            throw new InvalidOperationException(
                $"Service '{serviceName}' chưa được đăng ký trong ServiceRegistry. Hãy chạy ServiceRegistry trước, sau đó chạy {serviceName}.");
        }
    }
}
