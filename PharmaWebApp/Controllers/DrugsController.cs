using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// Controller quản lý giao diện Thuốc
    /// Gọi DrugService API để thực hiện các thao tác CRUD
    /// </summary>
    [Authorize(Policy = "OwnerOnly")]
    public class DrugsController : BaseController
    {
        private readonly ILogger<DrugsController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public DrugsController(ILogger<DrugsController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        private async Task<List<CategoryViewModel>> GetCategoriesAsync()
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();
                return await httpClient.GetFromJsonAsync<List<CategoryViewModel>>($"{drugServiceUrl}/api/categories") ?? new();
            }
            catch
            {
                return new();
            }
        }

        /// <summary>
        /// Hiển thị danh sách thuốc (có phân trang)
        /// GET /Drugs?page=1&pageSize=12
        /// </summary>
        public async Task<IActionResult> Index(int page = 1, int pageSize = 12)
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                _logger.LogInformation($"Gọi DrugService tại: {drugServiceUrl}/api/drugs");

                // Gọi DrugService để lấy danh sách thuốc
                var response = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs");

                if (response.IsSuccessStatusCode)
                {
                    var drugs = await response.Content.ReadFromJsonAsync<List<DrugViewModel>>();
                    var allDrugs = drugs ?? new List<DrugViewModel>();
                    
                    // Phân trang
                    var pagedList = PagedList<DrugViewModel>.Create(allDrugs, page, pageSize);
                    return View(pagedList);
                }
                else
                {
                    _logger.LogWarning($"DrugService trả về status code: {response.StatusCode}");
                    ViewBag.ErrorMessage = "Không thể kết nối với DrugService. Vui lòng kiểm tra service đã chạy chưa.";
                    var emptyList = new List<DrugViewModel>();
                    var pagedList = PagedList<DrugViewModel>.Create(emptyList, 1, pageSize);
                    return View(pagedList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách thuốc: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                var emptyList = new List<DrugViewModel>();
                var pagedList = PagedList<DrugViewModel>.Create(emptyList, 1, pageSize);
                return View(pagedList);
            }
        }

        /// <summary>
        /// Hiển thị form thêm thuốc mới
        /// GET /Drugs/Create
        /// </summary>
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await GetCategoriesAsync();
            return View();
        }

        /// <summary>
        /// Xử lý thêm thuốc mới
        /// POST /Drugs/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDrugViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await GetCategoriesAsync();
                return View(model);
            }

            if (model.SellPrice <= 0 && model.BoxPrice <= 0)
            {
                ModelState.AddModelError("", "Vui lòng nhập ít nhất 1 giá bán (theo viên hoặc theo hộp)");
                ViewBag.Categories = await GetCategoriesAsync();
                return View(model);
            }

            // Kiểm tra file ảnh bắt buộc
            if (model.ImageFile == null || model.ImageFile.Length == 0)
            {
                ModelState.AddModelError("ImageFile", "Vui lòng chọn ảnh thuốc");
                return View(model);
            }

            try
            {
                // Kiểm tra file có phải ảnh không
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(model.ImageFile.FileName).ToLower();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ImageFile", "Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .gif)");
                    return View(model);
                }

                // Kiểm tra kích thước file (max 5MB)
                if (model.ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ImageFile", "Kích thước file không được vượt quá 5MB");
                    return View(model);
                }

                // Tạo tên file unique
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                
                // Đường dẫn lưu file trong wwwroot/images/drugs
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "drugs");
                
                // Tạo thư mục nếu chưa có
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);

                // Lưu file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(fileStream);
                }

                // Set imageUrl là đường dẫn tương đối
                var imageUrl = $"/images/drugs/{fileName}";
                
                _logger.LogInformation($"Đã upload ảnh: {fileName}");

                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                // Tạo object để gửi
                var drugData = new
                {
                    name = model.Name,
                    code = model.Code,
                    category = model.Category,
                    unit = model.Unit,
                    packSize = model.PackSize,
                    importPrice = model.ImportPrice,
                    sellPrice = model.SellPrice,
                    boxPrice = model.BoxPrice,
                    imageUrl = imageUrl
                };

                _logger.LogInformation($"Gửi request thêm thuốc: {model.Name}");

                // Gọi DrugService để thêm thuốc
                var response = await httpClient.PostAsJsonAsync($"{drugServiceUrl}/api/drugs", drugData);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Thêm thuốc thành công");
                    TempData["SuccessMessage"] = $"Đã thêm thuốc '{model.Name}' thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    // Nếu thất bại, xóa ảnh đã upload
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Thêm thuốc thất bại. Status: {response.StatusCode}, Error: {errorContent}");
                    ModelState.AddModelError("", $"Không thể thêm thuốc. Lỗi từ server: {response.StatusCode}");
                    ViewBag.Categories = await GetCategoriesAsync();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi thêm thuốc: {ex.Message}");
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                ViewBag.Categories = await GetCategoriesAsync();
                return View(model);
            }
        }

        /// <summary>
        /// Hiển thị chi tiết thuốc
        /// GET /Drugs/Details/{id}
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var drug = await response.Content.ReadFromJsonAsync<DrugViewModel>();
                    return View(drug);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    ViewBag.ErrorMessage = "Không thể lấy thông tin thuốc";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết thuốc: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                return View();
            }
        }

        /// <summary>
        /// Hiển thị form sửa thuốc
        /// GET /Drugs/Edit/{id}
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var drug = await response.Content.ReadFromJsonAsync<DrugViewModel>();
                    if (drug == null)
                    {
                        return NotFound();
                    }
                    var editModel = new CreateDrugViewModel
                    {
                        Id = drug.Id,
                        Name = drug.Name,
                        Code = drug.Code,
                        Category = drug.Category,
                        Unit = drug.Unit,
                        PackSize = drug.PackSize,
                        ImportPrice = drug.ImportPrice,
                        SellPrice = drug.SellPrice,
                        BoxPrice = drug.BoxPrice,
                        ImageUrl = drug.ImageUrl
                    };

                    ViewBag.Categories = await GetCategoriesAsync();
                    return View(editModel);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    ViewBag.ErrorMessage = "Không thể lấy thông tin thuốc";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết thuốc: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                return View();
            }
        }

        /// <summary>
        /// Xử lý sửa thuốc
        /// POST /Drugs/Edit/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreateDrugViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await GetCategoriesAsync();
                return View(model);
            }

            if (model.SellPrice <= 0 && model.BoxPrice <= 0)
            {
                ModelState.AddModelError("", "Vui lòng nhập ít nhất 1 giá bán (theo viên hoặc theo hộp)");
                ViewBag.Categories = await GetCategoriesAsync();
                return View(model);
            }

            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                string imageUrl = model.ImageUrl ?? "";

                // Nếu có file ảnh mới, upload nó
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(model.ImageFile.FileName).ToLower();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImageFile", "Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .gif)");
                        return View(model);
                    }

                    if (model.ImageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImageFile", "Kích thước file không được vượt quá 5MB");
                        return View(model);
                    }

                    var fileName = $"{Guid.NewGuid()}{fileExtension}";
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "drugs");
                    
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(fileStream);
                    }

                    imageUrl = $"/images/drugs/{fileName}";
                }

                var drugData = new
                {
                    id = model.Id,
                    name = model.Name,
                    code = model.Code,
                    category = model.Category,
                    unit = model.Unit,
                    packSize = model.PackSize,
                    importPrice = model.ImportPrice,
                    sellPrice = model.SellPrice,
                    boxPrice = model.BoxPrice,
                    imageUrl = imageUrl
                };

                var response = await httpClient.PutAsJsonAsync($"{drugServiceUrl}/api/drugs/{id}", drugData);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Sửa thuốc thành công");
                    TempData["SuccessMessage"] = $"Đã sửa thuốc '{model.Name}' thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Sửa thuốc thất bại. Status: {response.StatusCode}, Error: {errorContent}");
                    ModelState.AddModelError("", $"Không thể sửa thuốc. Lỗi từ server: {response.StatusCode}");
                    ViewBag.Categories = await GetCategoriesAsync();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi sửa thuốc: {ex.Message}");
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                ViewBag.Categories = await GetCategoriesAsync();
                return View(model);
            }
        }

        /// <summary>
        /// Hiển thị form xác nhận xóa thuốc
        /// GET /Drugs/Delete/{id}
        /// </summary>
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var drug = await response.Content.ReadFromJsonAsync<DrugViewModel>();
                    return View(drug);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    ViewBag.ErrorMessage = "Không thể lấy thông tin thuốc";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết thuốc: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                return View();
            }
        }

        /// <summary>
        /// Xử lý xóa thuốc
        /// POST /Drugs/Delete/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.DeleteAsync($"{drugServiceUrl}/api/drugs/{id}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Đã xóa thuốc {id}");
                    TempData["SuccessMessage"] = $"Đã xóa thuốc thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Không thể xóa thuốc {id}: {errorContent}");
                    TempData["ErrorMessage"] = $"Không thể xóa thuốc. Lỗi: {errorContent}";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xóa thuốc: {ex.Message}");
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}

