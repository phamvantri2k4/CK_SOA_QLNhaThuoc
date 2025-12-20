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
        private readonly ILogger<EmployeesController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public EmployeesController(ILogger<EmployeesController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var authServiceUrl = await _serviceResolver.GetRequiredAsync("AuthService");
                var httpClient = CreateAuthenticatedHttpClient();

                var users = await httpClient.GetFromJsonAsync<List<EmployeeListItemViewModel>>($"{authServiceUrl}/api/users") ?? new();
                users = users.OrderByDescending(u => u.IsActive).ThenBy(u => u.Role).ThenBy(u => u.Username).ToList();
                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi lấy danh sách nhân viên: {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<EmployeeListItemViewModel>());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateEmployeeViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateEmployeeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var authServiceUrl = await _serviceResolver.GetRequiredAsync("AuthService");
                var httpClient = CreateAuthenticatedHttpClient();

                var payload = new
                {
                    username = model.Username,
                    password = model.Password,
                    fullName = model.FullName,
                    role = model.Role,
                    isActive = model.IsActive
                };

                var resp = await httpClient.PostAsJsonAsync($"{authServiceUrl}/api/users", payload);
                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã tạo nhân viên";
                    return RedirectToAction(nameof(Index));
                }

                var error = await resp.Content.ReadAsStringAsync();
                ModelState.AddModelError("", error);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi tạo nhân viên: {ex.Message}");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var authServiceUrl = await _serviceResolver.GetRequiredAsync("AuthService");
                var httpClient = CreateAuthenticatedHttpClient();

                var user = await httpClient.GetFromJsonAsync<EmployeeListItemViewModel>($"{authServiceUrl}/api/users/{id}");
                if (user == null) return NotFound();

                var vm = new EditEmployeeViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Role = user.Role,
                    IsActive = user.IsActive
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi lấy nhân viên #{id}: {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditEmployeeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var authServiceUrl = await _serviceResolver.GetRequiredAsync("AuthService");
                var httpClient = CreateAuthenticatedHttpClient();

                var payload = new
                {
                    fullName = model.FullName,
                    role = model.Role,
                    isActive = model.IsActive,
                    password = string.IsNullOrWhiteSpace(model.NewPassword) ? null : model.NewPassword
                };

                var resp = await httpClient.PutAsJsonAsync($"{authServiceUrl}/api/users/{model.Id}", payload);
                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã cập nhật nhân viên";
                    return RedirectToAction(nameof(Index));
                }

                var error = await resp.Content.ReadAsStringAsync();
                ModelState.AddModelError("", error);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi cập nhật nhân viên #{model.Id}: {ex.Message}");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var authServiceUrl = await _serviceResolver.GetRequiredAsync("AuthService");
                var httpClient = CreateAuthenticatedHttpClient();

                var resp = await httpClient.DeleteAsync($"{authServiceUrl}/api/users/{id}");
                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã xóa nhân viên";
                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] = await resp.Content.ReadAsStringAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi xóa nhân viên #{id}: {ex.Message}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
