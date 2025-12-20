namespace Shared
{
    /// <summary>
    /// Model đại diện cho thông tin một service trong Service Registry
    /// Được dùng để đăng ký (Publish) và tìm kiếm (Find) service
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// Tên của service (ví dụ: "DrugService", "SaleService")
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// URL endpoint của service (ví dụ: "https://localhost:5001")
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Mô tả về service (tùy chọn)
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Phiên bản của service (tùy chọn)
        /// </summary>
        public string? Version { get; set; }
    }
}

