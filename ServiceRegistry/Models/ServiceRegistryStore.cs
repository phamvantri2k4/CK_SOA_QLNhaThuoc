using Shared;

namespace ServiceRegistry.Models
{
    /// <summary>
    /// Lưu trữ danh sách các service đã đăng ký trong bộ nhớ (In-Memory)
    /// Trong thực tế, có thể thay bằng database để persistent storage
    /// </summary>
    public class ServiceRegistryStore
    {
        private sealed class ServiceEntry
        {
            public required ServiceInfo Info { get; init; }
            public DateTime LastHeartbeatUtc { get; set; }
        }

        private readonly TimeSpan _ttl;

        // Dictionary để lưu service: Key = ServiceName
        private readonly Dictionary<string, ServiceEntry> _services = new();

        // Lock object để đảm bảo thread-safe khi đọc/ghi
        private readonly object _lock = new();

        public ServiceRegistryStore(TimeSpan ttl)
        {
            _ttl = ttl;
        }

        /// <summary>
        /// Đăng ký hoặc cập nhật thông tin service
        /// </summary>
        public void Register(ServiceInfo serviceInfo)
        {
            lock (_lock)
            {
                PurgeExpiredUnsafe();

                _services[serviceInfo.ServiceName] = new ServiceEntry
                {
                    Info = serviceInfo,
                    LastHeartbeatUtc = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả các service đã đăng ký
        /// </summary>
        public List<ServiceInfo> GetAll()
        {
            lock (_lock)
            {
                PurgeExpiredUnsafe();
                return _services.Values.Select(x => x.Info).ToList();
            }
        }

        /// <summary>
        /// Tìm service theo tên
        /// </summary>
        public ServiceInfo? FindByName(string serviceName)
        {
            lock (_lock)
            {
                PurgeExpiredUnsafe();
                return _services.TryGetValue(serviceName, out var entry) ? entry.Info : null;
            }
        }

        public bool Touch(string serviceName)
        {
            lock (_lock)
            {
                PurgeExpiredUnsafe();
                if (!_services.TryGetValue(serviceName, out var entry)) return false;
                entry.LastHeartbeatUtc = DateTime.UtcNow;
                return true;
            }
        }

        /// <summary>
        /// Xóa service khỏi registry (tùy chọn, để mở rộng sau)
        /// </summary>
        public bool Remove(string serviceName)
        {
            lock (_lock)
            {
                PurgeExpiredUnsafe();
                return _services.Remove(serviceName);
            }
        }

        public void PurgeExpired()
        {
            lock (_lock)
            {
                PurgeExpiredUnsafe();
            }
        }

        private void PurgeExpiredUnsafe()
        {
            if (_ttl <= TimeSpan.Zero) return;

            var now = DateTime.UtcNow;
            var expired = _services
                .Where(kvp => now - kvp.Value.LastHeartbeatUtc > _ttl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                _services.Remove(key);
            }
        }
    }
}

