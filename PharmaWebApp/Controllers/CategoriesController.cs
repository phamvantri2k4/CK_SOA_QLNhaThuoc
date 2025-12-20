using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class CategoriesController : BaseController
    {
        private readonly ILogger<CategoriesController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public CategoriesController(ILogger<CategoriesController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var categories = await httpClient.GetFromJsonAsync<List<CategoryViewModel>>($"{drugServiceUrl}/api/categories") ?? new();
                categories = categories.OrderBy(c => c.Name).ToList();
                return View(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách danh mục: {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<CategoryViewModel>());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CategoryViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var payload = new { name = model.Name };
                var resp = await httpClient.PostAsJsonAsync($"{drugServiceUrl}/api/categories", payload);

                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã tạo danh mục";
                    return RedirectToAction(nameof(Index));
                }

                var error = await resp.Content.ReadAsStringAsync();
                ModelState.AddModelError("", error);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi tạo danh mục: {ex.Message}");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var categories = await httpClient.GetFromJsonAsync<List<CategoryViewModel>>($"{drugServiceUrl}/api/categories") ?? new();
                var category = categories.FirstOrDefault(c => c.Id == id);
                if (category == null) return NotFound();

                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi lấy danh mục #{id}: {ex.Message}");
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryViewModel model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid) return View(model);

            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var payload = new { name = model.Name };
                var resp = await httpClient.PutAsJsonAsync($"{drugServiceUrl}/api/categories/{id}", payload);

                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã cập nhật danh mục";
                    return RedirectToAction(nameof(Index));
                }

                var error = await resp.Content.ReadAsStringAsync();
                ModelState.AddModelError("", error);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi cập nhật danh mục #{id}: {ex.Message}");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var categories = await httpClient.GetFromJsonAsync<List<CategoryViewModel>>($"{drugServiceUrl}/api/categories") ?? new();
                var category = categories.FirstOrDefault(c => c.Id == id);
                if (category == null) return NotFound();

                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi lấy danh mục #{id}: {ex.Message}");
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var resp = await httpClient.DeleteAsync($"{drugServiceUrl}/api/categories/{id}");
                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã xóa danh mục";
                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] = await resp.Content.ReadAsStringAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi xóa danh mục #{id}: {ex.Message}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
