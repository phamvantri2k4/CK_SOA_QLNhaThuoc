using Microsoft.AspNetCore.Mvc;
using ServiceRegistry.Models;
using Shared;

namespace ServiceRegistry.Controllers
{
    [ApiController]
    [Route("api/registry")]
    public class RegistryController : ControllerBase
    {
        private readonly ServiceRegistryStore _store;
        private readonly ILogger<RegistryController> _logger;

        public RegistryController(ServiceRegistryStore store, ILogger<RegistryController> logger)
        {
            _store = store;
            _logger = logger;
        }

        /* ================= REGISTER ================= */

        [HttpPost("register")]
        public IActionResult Register(ServiceInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.ServiceName) || string.IsNullOrWhiteSpace(info.Url))
                return BadRequest("ServiceName và Url là bắt buộc");

            _store.Register(info);
            _logger.LogInformation($"Service '{info.ServiceName}' đăng ký tại {info.Url}");

            return Ok(new { message = "Registered", info.ServiceName, info.Url });
        }

        /* ================= DISCOVERY ================= */

        [HttpGet("services")]
        public IActionResult GetAll()
        {
            return Ok(_store.GetAll());
        }

        [HttpGet("services/{name}")]
        public IActionResult Find(string name)
        {
            var service = _store.FindByName(name);
            return service == null
                ? NotFound($"Service '{name}' không tồn tại")
                : Ok(service);
        }

        /* ================= HEARTBEAT ================= */

        [HttpPost("heartbeat/{name}")]
        public IActionResult Heartbeat(string name)
        {
            return _store.Touch(name)
                ? Ok(new { message = "Heartbeat OK", service = name })
                : NotFound($"Service '{name}' không tồn tại");
        }

        /* ================= DELETE ================= */

        [HttpDelete("services/{name}")]
        public IActionResult Delete(string name)
        {
            return _store.Remove(name)
                ? Ok($"Service '{name}' đã bị xóa")
                : NotFound($"Service '{name}' không tồn tại");
        }

        /* ================= HEALTH ================= */

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "ok" });
        }
    }
}
