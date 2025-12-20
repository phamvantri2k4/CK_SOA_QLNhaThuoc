using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using Shared;
using System.Diagnostics;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// Controller chính cho trang Home - hiển thị thông tin tổng quan
    /// </summary>
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ServiceDiscoveryClient _discoveryClient;

        public HomeController(
            ILogger<HomeController> logger,
            ServiceDiscoveryClient discoveryClient)
        {
            _logger = logger;
            _discoveryClient = discoveryClient;
        }

        /// <summary>
        /// Trang chủ - hiển thị danh sách service đã đăng ký
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                // Lấy danh sách service từ ServiceRegistry
                var services = await _discoveryClient.GetAllServicesAsync();
                return View(services);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách service: {ex.Message}");
                return View(new List<ServiceInfo>());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
