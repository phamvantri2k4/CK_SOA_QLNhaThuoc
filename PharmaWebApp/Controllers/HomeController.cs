using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using Shared.Services;
using System.Diagnostics;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// Trang Home - hiển thị thông tin tổng quan hệ thống
    /// </summary>
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly ConsulServiceDiscovery _consul;

        public HomeController(ConsulServiceDiscovery consul)
        {
            _consul = consul;
        }

        /// <summary>
        /// Trang chủ - Dashboard hiển thị trạng thái các Service từ Consul
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var allServices = await _consul.GetAllServicesAsync();
            
            // Lọc ra danh sách các Service duy nhất theo tên (Service)
            // Bỏ qua các service hệ thống của Consul nếu muốn
            var displayServices = allServices
                .Where(s => s.Service != "consul") 
                .GroupBy(s => s.Service)
                .Select(g => g.First()) 
                .ToList();

            return View(displayServices);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
