using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class EmployeesController : BaseController
    {
        private readonly ServiceUrlHelper _serviceUrl;

        public EmployeesController(ServiceUrlHelper serviceUrl)
        {
            _serviceUrl = serviceUrl;
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> GetAuthClientAsync()
        {
            var url = await _serviceUrl.GetAuthServiceUrlAsync();
            return (CreateAuthenticatedHttpClient(), url);
        }

        /* ================= DANH SÁCH ================= */

        public async Task<IActionResult> Index()
        {
            var (client, url) = await GetAuthClientAsync();

            var users = await client.GetFromJsonAsync<List<EmployeeListItemViewModel>>(
                $"{url}/api/users") ?? new();

            return View(
                users.OrderByDescending(u => u.IsActive)
                     .ThenBy(u => u.Role)
                     .ThenBy(u => u.Username)
                     .ToList()
            );
        }

        /* ================= CREATE ================= */

        public IActionResult Create()
        {
            return View(new CreateEmployeeViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateEmployeeViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var (client, url) = await GetAuthClientAsync();

            await client.PostAsJsonAsync($"{url}/api/users", new
            {
                m.Username,
                m.Password,
                m.FullName,
                m.Role,
                m.IsActive
            });

            TempData["SuccessMessage"] = "Đã tạo nhân viên";
            return RedirectToAction(nameof(Index));
        }

        /* ================= EDIT ================= */

        public async Task<IActionResult> Edit(int id)
        {
            var (client, url) = await GetAuthClientAsync();

            var user = await client.GetFromJsonAsync<EmployeeListItemViewModel>(
                $"{url}/api/users/{id}");

            if (user == null) return NotFound();

            return View(new EditEmployeeViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditEmployeeViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var (client, url) = await GetAuthClientAsync();

            await client.PutAsJsonAsync($"{url}/api/users/{m.Id}", new
            {
                m.FullName,
                m.Role,
                m.IsActive,
                password = string.IsNullOrWhiteSpace(m.NewPassword)
                    ? null
                    : m.NewPassword
            });

            TempData["SuccessMessage"] = "Đã cập nhật nhân viên";
            return RedirectToAction(nameof(Index));
        }

        /* ================= DELETE ================= */

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (client, url) = await GetAuthClientAsync();
            await client.DeleteAsync($"{url}/api/users/{id}");

            TempData["SuccessMessage"] = "Đã xóa nhân viên";
            return RedirectToAction(nameof(Index));
        }
    }
}
