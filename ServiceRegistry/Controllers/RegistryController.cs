using Microsoft.AspNetCore.Mvc;
using ServiceRegistry.Models;
using Shared;

namespace ServiceRegistry.Controllers
{
    /// <summary>
    /// Controller xử lý các yêu cầu đăng ký và tìm kiếm service
    /// Đây là trung tâm của Service Registry trong kiến trúc SOA
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RegistryController : ControllerBase
    {
        private readonly ServiceRegistryStore _store;
        private readonly ILogger<RegistryController> _logger;

        public RegistryController(ServiceRegistryStore store, ILogger<RegistryController> logger)
        {
            _store = store;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint để service tự đăng ký khi khởi động (Publish)
        /// POST /api/registry/register
        /// </summary>
        /// <param name="serviceInfo">Thông tin service cần đăng ký</param>
        /// <returns>200 OK nếu thành công</returns>
        [HttpPost("register")]
        public IActionResult Register([FromBody] ServiceInfo serviceInfo)
        {
            // Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrWhiteSpace(serviceInfo.ServiceName))
            {
                _logger.LogWarning("Đăng ký service thất bại: ServiceName không được để trống");
                return BadRequest(new { message = "ServiceName không được để trống" });
            }

            if (string.IsNullOrWhiteSpace(serviceInfo.Url))
            {
                _logger.LogWarning("Đăng ký service thất bại: Url không được để trống");
                return BadRequest(new { message = "Url không được để trống" });
            }

            // Đăng ký service vào store
            _store.Register(serviceInfo);
            
            _logger.LogInformation($"Service '{serviceInfo.ServiceName}' đã đăng ký thành công tại {serviceInfo.Url}");

            return Ok(new 
            { 
                message = "Đăng ký service thành công", 
                serviceName = serviceInfo.ServiceName,
                url = serviceInfo.Url
            });
        }

        /// <summary>
        /// Lấy danh sách tất cả các service đã đăng ký
        /// GET /api/registry/services
        /// </summary>
        /// <returns>Danh sách ServiceInfo</returns>
        [HttpGet("services")]
        public IActionResult GetAllServices()
        {
            var services = _store.GetAll();
            
            _logger.LogInformation($"Trả về danh sách {services.Count} service(s)");

            return Ok(services);
        }

        /// <summary>
        /// Tìm service theo tên (Find)
        /// GET /api/registry/services/{serviceName}
        /// </summary>
        /// <param name="serviceName">Tên service cần tìm</param>
        /// <returns>ServiceInfo nếu tìm thấy, 404 nếu không tìm thấy</returns>
        [HttpGet("services/{serviceName}")]
        public IActionResult FindService(string serviceName)
        {
            var service = _store.FindByName(serviceName);

            if (service == null)
            {
                _logger.LogWarning($"Không tìm thấy service '{serviceName}'");
                return NotFound(new { message = $"Không tìm thấy service '{serviceName}'" });
            }

            _logger.LogInformation($"Tìm thấy service '{serviceName}' tại {service.Url}");

            return Ok(service);
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "ok" });
        }

        [HttpPost("heartbeat/{serviceName}")]
        public IActionResult Heartbeat(string serviceName)
        {
            var ok = _store.Touch(serviceName);
            if (!ok)
            {
                _logger.LogWarning($"Heartbeat cho service '{serviceName}' thất bại - không tồn tại hoặc đã hết hạn");
                return NotFound(new { message = $"Service '{serviceName}' không tồn tại" });
            }

            return Ok(new { message = "Heartbeat OK", serviceName });
        }

        /// <summary>
        /// Xóa service khỏi registry (tùy chọn, để mở rộng)
        /// DELETE /api/registry/services/{serviceName}
        /// </summary>
        [HttpDelete("services/{serviceName}")]
        public IActionResult DeleteService(string serviceName)
        {
            var result = _store.Remove(serviceName);

            if (!result)
            {
                _logger.LogWarning($"Không thể xóa service '{serviceName}' - không tồn tại");
                return NotFound(new { message = $"Service '{serviceName}' không tồn tại" });
            }

            _logger.LogInformation($"Service '{serviceName}' đã bị xóa khỏi registry");

            return Ok(new { message = $"Service '{serviceName}' đã bị xóa" });
        }
    }
}

