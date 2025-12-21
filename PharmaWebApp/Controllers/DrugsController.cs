using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class DrugsController : BaseController
    {
        private readonly IServiceResolver _resolver;

        public DrugsController(IServiceResolver resolver)
        {
            _resolver = resolver;
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> GetClientAsync()
        {
            var url = await _resolver.GetRequiredAsync("DrugService");
            return (CreateAuthenticatedHttpClient(), url);
        }

        private bool CheckPrice(CreateDrugViewModel m)
        {
            if (m.BoxPrice <= 0 && m.SellPricePerPill <= 0)
            {
                ModelState.AddModelError("", "Nhập ít nhất 1 giá bán");
                return false;
            }
            return true;
        }

        private async Task<string?> SaveImageAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLower();
            var allow = new[] { ".jpg", ".jpeg", ".png", ".gif" };

            if (!allow.Contains(ext) || file.Length > 5 * 1024 * 1024)
                return null;

            var folder = Path.Combine("wwwroot", "images", "drugs");
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var path = Path.Combine(folder, fileName);

            using var fs = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(fs);

            return $"/images/drugs/{fileName}";
        }

        private async Task<List<CategoryViewModel>> GetCategoriesAsync()
        {
            var (client, url) = await GetClientAsync();
            return await client.GetFromJsonAsync<List<CategoryViewModel>>(
                $"{url}/api/categories") ?? new();
        }

        /* ================= INDEX ================= */

        public async Task<IActionResult> Index(int page = 1, int pageSize = 12)
        {
            try
            {
                var (client, url) = await GetClientAsync();
                var drugs = await client.GetFromJsonAsync<List<DrugViewModel>>(
                    $"{url}/api/drugs") ?? new();

                return View(PagedList<DrugViewModel>.Create(drugs, page, pageSize));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải danh sách thuốc: {ex.Message}";
                return View(PagedList<DrugViewModel>.Create(new List<DrugViewModel>(), page, pageSize));
            }
        }

        /* ================= DETAILS ================= */

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var (client, url) = await GetClientAsync();
                var drug = await client.GetFromJsonAsync<DrugViewModel>(
                    $"{url}/api/drugs/{id}");

                if (drug == null) return NotFound();
                return View(drug);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải thông tin thuốc: {ex.Message}";
                TempData["ErrorMessage"] = $"Lỗi khi tải thông tin thuốc: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /* ================= CREATE ================= */

        public async Task<IActionResult> Create()
        {
            try
            {
                ViewBag.Categories = await GetCategoriesAsync();
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải form tạo thuốc: {ex.Message}";
                ViewBag.Categories = new List<CategoryViewModel>();
                return View();
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDrugViewModel m)
        {
            if (!ModelState.IsValid || !CheckPrice(m))
            {
                ViewBag.Categories = await GetCategoriesAsync();
                return View(m);
            }

            if (m.ImageFile == null)
            {
                ModelState.AddModelError("ImageFile", "Chọn ảnh thuốc");
                return View(m);
            }

            var imageUrl = await SaveImageAsync(m.ImageFile);
            if (imageUrl == null)
            {
                ModelState.AddModelError("ImageFile", "Ảnh không hợp lệ");
                return View(m);
            }

            var (client, url) = await GetClientAsync();
            await client.PostAsJsonAsync($"{url}/api/drugs", new
            {
                m.Name,
                m.Code,
                m.Category,
                m.Unit,
                m.PackSize,
                m.ImportPrice,
                m.SellPricePerPill,
                m.BoxPrice,
                imageUrl
            });

            TempData["SuccessMessage"] = "Thêm thuốc thành công";
            return RedirectToAction(nameof(Index));
        }

        /* ================= EDIT ================= */

        public async Task<IActionResult> Edit(int id)
        {
            var (client, url) = await GetClientAsync();
            var drug = await client.GetFromJsonAsync<DrugViewModel>(
                $"{url}/api/drugs/{id}");

            if (drug == null) return NotFound();

            ViewBag.Categories = await GetCategoriesAsync();

            return View(new CreateDrugViewModel
            {
                Id = drug.Id,
                Name = drug.Name,
                Code = drug.Code,
                Category = drug.Category,
                Unit = drug.Unit,
                PackSize = drug.PackSize,
                ImportPrice = drug.ImportPrice,
                SellPricePerPill = drug.SellPricePerPill,
                BoxPrice = drug.BoxPrice,
                ImageUrl = drug.ImageUrl
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreateDrugViewModel m)
        {
            if (id != m.Id || !ModelState.IsValid || !CheckPrice(m))
            {
                ViewBag.Categories = await GetCategoriesAsync();
                return View(m);
            }

            var imageUrl = m.ImageUrl;
            if (m.ImageFile != null)
            {
                imageUrl = await SaveImageAsync(m.ImageFile) ?? imageUrl;
            }

            var (client, url) = await GetClientAsync();
            await client.PutAsJsonAsync($"{url}/api/drugs/{id}", new
            {
                m.Id,
                m.Name,
                m.Code,
                m.Category,
                m.Unit,
                m.PackSize,
                m.ImportPrice,
                m.SellPricePerPill,
                m.BoxPrice,
                imageUrl
            });

            TempData["SuccessMessage"] = "Sửa thuốc thành công";
            return RedirectToAction(nameof(Index));
        }

        /* ================= DELETE ================= */

        public async Task<IActionResult> Delete(int id)
        {
            var (client, url) = await GetClientAsync();
            var drug = await client.GetFromJsonAsync<DrugViewModel>(
                $"{url}/api/drugs/{id}");

            if (drug == null) return NotFound();
            return View(drug);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var (client, url) = await GetClientAsync();
            await client.DeleteAsync($"{url}/api/drugs/{id}");

            TempData["SuccessMessage"] = "Đã xóa thuốc";
            return RedirectToAction(nameof(Index));
        }
    }
}
