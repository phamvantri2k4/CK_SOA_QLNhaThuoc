using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using Shared;
using System.Diagnostics;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// Trang Home - hiển thị thông tin tổng quan hệ thống
    /// </summary>
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly ServiceDiscoveryClient _discoveryClient;

        public HomeController(ServiceDiscoveryClient discoveryClient)
        {
            _discoveryClient = discoveryClient;
        }

        /// <summary>
        /// Trang chủ - danh sách các service đã đăng ký vào Registry
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var services = await _discoveryClient.GetAllServicesAsync();
            return View(services);
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
