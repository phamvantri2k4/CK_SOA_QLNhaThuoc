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
        private readonly IServiceResolver _resolver;

        public CategoriesController(IServiceResolver resolver)
        {
            _resolver = resolver;
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> GetClientAsync()
        {
            var url = await _resolver.GetRequiredAsync("DrugService");
            return (CreateAuthenticatedHttpClient(), url);
        }

        /* ================= DANH SÁCH ================= */

        public async Task<IActionResult> Index()
        {
            try
            {
                var (client, url) = await GetClientAsync();

                var categories = await client.GetFromJsonAsync<List<CategoryViewModel>>(
                    $"{url}/api/categories") ?? new();

                return View(categories.OrderBy(c => c.Name).ToList());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải danh mục: {ex.Message}";
                return View(new List<CategoryViewModel>());
            }
        }

        /* ================= CREATE ================= */

        public IActionResult Create()
        {
            return View(new CategoryViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var (client, url) = await GetClientAsync();

            var res = await client.PostAsJsonAsync(
                $"{url}/api/categories",
                new { name = m.Name });

            if (!res.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Không thể tạo danh mục");
                return View(m);
            }

            TempData["SuccessMessage"] = "Đã tạo danh mục";
            return RedirectToAction(nameof(Index));
        }

        /* ================= EDIT ================= */

        public async Task<IActionResult> Edit(int id)
        {
            var (client, url) = await GetClientAsync();

            var categories = await client.GetFromJsonAsync<List<CategoryViewModel>>(
                $"{url}/api/categories") ?? new();

            var category = categories.FirstOrDefault(c => c.Id == id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryViewModel m)
        {
            if (id != m.Id || !ModelState.IsValid)
                return View(m);

            var (client, url) = await GetClientAsync();

            var res = await client.PutAsJsonAsync(
                $"{url}/api/categories/{id}",
                new { name = m.Name });

            if (!res.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Không thể cập nhật danh mục");
                return View(m);
            }

            TempData["SuccessMessage"] = "Đã cập nhật danh mục";
            return RedirectToAction(nameof(Index));
        }

        /* ================= DELETE ================= */

        public async Task<IActionResult> Delete(int id)
        {
            var (client, url) = await GetClientAsync();

            var categories = await client.GetFromJsonAsync<List<CategoryViewModel>>(
                $"{url}/api/categories") ?? new();

            var category = categories.FirstOrDefault(c => c.Id == id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var (client, url) = await GetClientAsync();

            await client.DeleteAsync($"{url}/api/categories/{id}");

            TempData["SuccessMessage"] = "Đã xóa danh mục";
            return RedirectToAction(nameof(Index));
        }
    }
}
