using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// Controller quản lý giao diện Hóa đơn bán hàng
    /// Gọi SaleService và DrugService để lập hóa đơn
    /// </summary>
    [Authorize(Policy = "StaffOrOwner")]
    public class SalesController : BaseController
    {
        private readonly ILogger<SalesController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public SalesController(ILogger<SalesController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        /// <summary>
        /// Hiển thị danh sách hóa đơn (có phân trang và filter theo nhân viên)
        /// GET /Sales?page=1&pageSize=10&staffId=1
        /// </summary>
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, int? staffId = null)
        {
            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                // Lấy danh sách nhân viên để hiển thị trong dropdown filter
                var staffList = await GetStaffListAsync();
                ViewBag.StaffList = staffList;
                ViewBag.SelectedStaffId = staffId;

                // Tạo URL với filter staffId nếu có
                var salesUrl = $"{saleServiceUrl}/api/sales";
                if (staffId.HasValue && staffId.Value > 0)
                {
                    salesUrl += $"?staffId={staffId.Value}";
                }

                _logger.LogInformation($"Gọi SaleService tại: {salesUrl}");

                // Gọi SaleService để lấy danh sách hóa đơn
                var response = await httpClient.GetAsync(salesUrl);

                if (response.IsSuccessStatusCode)
                {
                    var invoices = await response.Content.ReadFromJsonAsync<List<SaleInvoiceDisplayViewModel>>();
                    var allInvoices = invoices ?? new List<SaleInvoiceDisplayViewModel>();
                    
                    // Sắp xếp theo ngày mới nhất
                    allInvoices = allInvoices.OrderByDescending(i => i.CreatedAt).ToList();
                    
                    // Phân trang
                    var pagedList = PagedList<SaleInvoiceDisplayViewModel>.Create(allInvoices, page, pageSize);
                    return View(pagedList);
                }
                else
                {
                    _logger.LogWarning($"SaleService trả về status code: {response.StatusCode}");
                    ViewBag.ErrorMessage = "Không thể kết nối với SaleService. Vui lòng kiểm tra service đã chạy chưa.";
                    var emptyList = new List<SaleInvoiceDisplayViewModel>();
                    var pagedList = PagedList<SaleInvoiceDisplayViewModel>.Create(emptyList, 1, pageSize);
                    return View(pagedList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách hóa đơn: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                var emptyList = new List<SaleInvoiceDisplayViewModel>();
                var pagedList = PagedList<SaleInvoiceDisplayViewModel>.Create(emptyList, 1, pageSize);
                return View(pagedList);
            }
        }

        private class InventoryItemDto
        {
            public int Id { get; set; }
            public int DrugId { get; set; }
            public int Quantity { get; set; }
            public DateTime? ExpiryDate { get; set; }
        }

        private Task<string?> ResolveServiceUrlOptionalAsync(string serviceName)
        {
            return _serviceResolver.GetOptionalAsync(serviceName);
        }

        /// <summary>
        /// Hiển thị form tạo hóa đơn mới
        /// GET /Sales/Create
        /// </summary>
        public async Task<IActionResult> Create()
        {
            // Lấy danh sách thuốc để hiển thị trong dropdown
            var drugs = await GetDrugsFromServiceAsync();
            ViewBag.Drugs = drugs;

            return View(new CreateSaleViewModel());
        }

        /// <summary>
        /// Hiển thị giao diện lập hóa đơn mới (dạng shop)
        /// GET /Sales/OrderCreate
        /// </summary>
        public async Task<IActionResult> OrderCreate()
        {
            // Lấy danh sách thuốc
            var drugs = await GetDrugsFromServiceAsync();
            ViewBag.Drugs = drugs;

            ViewBag.Categories = drugs
                .Select(d => d.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            Dictionary<int, int> stockByDrugId = new();
            try
            {
                var inventoryBaseUrl = await ResolveServiceUrlOptionalAsync("InventoryService");
                if (!string.IsNullOrWhiteSpace(inventoryBaseUrl))
                {
                    var httpClient = CreateAuthenticatedHttpClient();
                    var items = await httpClient.GetFromJsonAsync<List<InventoryItemDto>>($"{inventoryBaseUrl}/api/inventory/status") ?? new();
                    stockByDrugId = items
                        .GroupBy(x => x.DrugId)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Không thể lấy tồn kho từ InventoryService để hiển thị trên màn lập hóa đơn: {ex.Message}");
            }

            ViewBag.StockByDrugId = stockByDrugId;

            return View();
        }

        /// <summary>
        /// Xử lý đặt hàng từ giỏ hàng (AJAX)
        /// POST /Sales/SubmitOrder
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitOrder([FromBody] CreateSaleViewModel model)
        {
            try
            {
                // Validate
                if (model.Items == null || !model.Items.Any())
                {
                    return BadRequest("Giỏ hàng trống");
                }

                if (string.IsNullOrWhiteSpace(model.CustomerName) || string.IsNullOrWhiteSpace(model.CustomerPhone))
                {
                    return BadRequest(new { message = "Vui lòng nhập đầy đủ Tên khách hàng và Số điện thoại trước khi tạo đơn hàng" });
                }

                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                // Lấy StaffId từ claims
                var staffIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var staffId = int.TryParse(staffIdClaim, out var id) ? id : 1;

                // Tạo request data
                var saleRequest = new
                {
                    customerName = model.CustomerName,
                    customerPhone = model.CustomerPhone,
                    staffId = staffId,
                    items = model.Items.Select(i => new
                    {
                        drugId = i.DrugId,
                        unitType = i.UnitType,
                        quantity = i.Quantity
                    }).ToList()
                };

                _logger.LogInformation($"Gửi request tạo hóa đơn với {model.Items.Count} items. DrugIds: {string.Join(", ", model.Items.Select(i => i.DrugId))}");

                // Gọi SaleService
                var response = await httpClient.PostAsJsonAsync($"{saleServiceUrl}/api/sales/create", saleRequest);

                if (response.IsSuccessStatusCode)
                {
                    var invoice = await response.Content.ReadFromJsonAsync<SaleInvoiceDisplayViewModel>();
                    _logger.LogInformation($"Tạo hóa đơn thành công. ID: {invoice?.Id}");
                    
                    return Ok(invoice);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Tạo hóa đơn thất bại: {errorContent}");
                    return BadRequest(errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tạo hóa đơn: {ex.Message}");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Xử lý tạo hóa đơn mới (đơn giản - 1 thuốc)
        /// POST /Sales/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string? customerName, int drugId, int quantity)
        {
            try
            {
                // Validate
                if (drugId <= 0)
                {
                    ModelState.AddModelError("", "Vui lòng chọn thuốc");
                    ViewBag.Drugs = await GetDrugsFromServiceAsync();
                    return View();
                }

                if (quantity <= 0)
                {
                    ModelState.AddModelError("", "Số lượng phải lớn hơn 0");
                    ViewBag.Drugs = await GetDrugsFromServiceAsync();
                    return View();
                }

                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                // Lấy StaffId từ claims
                var staffIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var staffId = int.TryParse(staffIdClaim, out var sid) ? sid : 1;

                // Tạo request data
                var saleRequest = new
                {
                    customerName = customerName ?? "Khách vãng lai",
                    staffId = staffId,
                    items = new[]
                    {
                        new
                        {
                            drugId = drugId,
                            quantity = quantity
                        }
                    }
                };

                _logger.LogInformation($"Gửi request tạo hóa đơn với {quantity} thuốc ID {drugId}");

                // Gọi SaleService để tạo hóa đơn
                var response = await httpClient.PostAsJsonAsync($"{saleServiceUrl}/api/sales/create", saleRequest);

                if (response.IsSuccessStatusCode)
                {
                    var invoice = await response.Content.ReadFromJsonAsync<SaleInvoiceDisplayViewModel>();
                    _logger.LogInformation($"Tạo hóa đơn thành công. ID: {invoice?.Id}");
                    TempData["SuccessMessage"] = $"Đã tạo hóa đơn #{invoice?.Id} thành công! Tổng tiền: {invoice?.TotalAmount:N0} VNĐ";
                    
                    // Chuyển sang trang chi tiết
                    return RedirectToAction(nameof(Details), new { id = invoice?.Id });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Tạo hóa đơn thất bại. Status: {response.StatusCode}, Error: {errorContent}");
                    ModelState.AddModelError("", $"Không thể tạo hóa đơn. Lỗi từ server: {errorContent}");
                    ViewBag.Drugs = await GetDrugsFromServiceAsync();
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tạo hóa đơn: {ex.Message}");
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                ViewBag.Drugs = await GetDrugsFromServiceAsync();
                return View();
            }
        }

        /// <summary>
        /// Đánh dấu hóa đơn đã thanh toán
        /// POST /Sales/MarkAsPaid/{id}
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.PutAsync($"{saleServiceUrl}/api/sales/{id}/pay", null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Đã đánh dấu hóa đơn {id} là đã thanh toán");
                    return Ok(new { message = "Thanh toán thành công" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Không thể thanh toán hóa đơn {id}: {errorContent}");
                    return BadRequest(errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi thanh toán hóa đơn: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Hiển thị chi tiết hóa đơn
        /// GET /Sales/Details/{id}
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.GetAsync($"{saleServiceUrl}/api/sales/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var invoice = await response.Content.ReadFromJsonAsync<SaleInvoiceDisplayViewModel>();
                    return View(invoice);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    ViewBag.ErrorMessage = "Không thể lấy thông tin hóa đơn";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết hóa đơn: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                return View();
            }
        }

        /// <summary>
        /// Helper method: Lấy danh sách thuốc từ DrugService
        /// </summary>
        private async Task<List<DrugViewModel>> GetDrugsFromServiceAsync()
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                _logger.LogInformation($"Gọi DrugService để lấy danh sách thuốc tại: {drugServiceUrl}/api/drugs");

                var response = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs");

                if (response.IsSuccessStatusCode)
                {
                    var drugs = await response.Content.ReadFromJsonAsync<List<DrugViewModel>>();
                    _logger.LogInformation($"Lấy được {drugs?.Count ?? 0} thuốc từ DrugService");
                    return drugs ?? new List<DrugViewModel>();
                }
                else
                {
                    _logger.LogWarning($"DrugService trả về status: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Error content: {errorContent}");
                }

                return new List<DrugViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách thuốc: {ex.Message}");
                return new List<DrugViewModel>();
            }
        }

        /// <summary>
        /// Lấy danh sách nhân viên từ AuthService để hiển thị trong filter
        /// </summary>
        private async Task<List<StaffViewModel>> GetStaffListAsync()
        {
            try
            {
                var authServiceUrl = await _serviceResolver.GetRequiredAsync("AuthService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.GetAsync($"{authServiceUrl}/api/users");

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<StaffViewModel>>();
                    return users ?? new List<StaffViewModel>();
                }
                else
                {
                    _logger.LogWarning($"Không thể lấy danh sách nhân viên. Status: {response.StatusCode}");
                    return new List<StaffViewModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách nhân viên: {ex.Message}");
                return new List<StaffViewModel>();
            }
        }

        /// <summary>
        /// Hiển thị form sửa hóa đơn (chỉ admin)
        /// GET /Sales/Edit/{id}
        /// </summary>
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.GetAsync($"{saleServiceUrl}/api/sales/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var invoice = await response.Content.ReadFromJsonAsync<SaleInvoiceDisplayViewModel>();
                    if (invoice == null) return NotFound();

                    var vm = new EditSaleViewModel
                    {
                        Id = invoice.Id,
                        CustomerName = invoice.CustomerName,
                        PaymentStatus = invoice.PaymentStatus,
                        Items = invoice.Items
                            .Select(i => new SaleItemViewModel
                            {
                                DrugId = i.DrugId,
                                UnitType = string.IsNullOrWhiteSpace(i.UnitType) ? "pill" : i.UnitType,
                                Quantity = i.Quantity
                            })
                            .ToList()
                    };

                    return View(vm);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Không thể lấy thông tin hóa đơn {id}. Status: {response.StatusCode}. Error: {errorContent}");
                    TempData["ErrorMessage"] = "Không thể lấy thông tin hóa đơn";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết hóa đơn: {ex.Message}");
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [Authorize(Policy = "OwnerOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditSaleViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (string.Equals(model.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Không thể sửa hóa đơn đã thanh toán");
            }

            if (model.Items == null || model.Items.Count == 0)
            {
                ModelState.AddModelError("", "Hóa đơn phải có ít nhất một sản phẩm");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                var updateRequest = new UpdateSaleRequestDto
                {
                    CustomerName = model.CustomerName,
                    CustomerPhone = model.CustomerPhone,
                    Items = model.Items
                };

                var response = await httpClient.PutAsJsonAsync($"{saleServiceUrl}/api/sales/{id}", updateRequest);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Đã cập nhật hóa đơn #{id} thành công";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Cập nhật hóa đơn {id} thất bại. Status: {response.StatusCode}. Error: {errorContent}");

                ModelState.AddModelError("", string.IsNullOrWhiteSpace(errorContent) ? "Không thể cập nhật hóa đơn" : errorContent);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi cập nhật hóa đơn {id}: {ex.Message}");
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                return View(model);
            }
        }

        /// <summary>
        /// Hiển thị form xác nhận xóa hóa đơn (chỉ admin)
        /// GET /Sales/Delete/{id}
        /// </summary>
        [Authorize(Policy = "OwnerOnly")]
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.GetAsync($"{saleServiceUrl}/api/sales/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var invoice = await response.Content.ReadFromJsonAsync<SaleInvoiceDisplayViewModel>();
                    return View(invoice);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    ViewBag.ErrorMessage = "Không thể lấy thông tin hóa đơn";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết hóa đơn: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                return View();
            }
        }

        /// <summary>
        /// Xử lý xóa hóa đơn (chỉ admin)
        /// POST /Sales/Delete/{id}
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "OwnerOnly")]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                var response = await httpClient.DeleteAsync($"{saleServiceUrl}/api/sales/{id}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Đã xóa hóa đơn {id}");
                    TempData["SuccessMessage"] = $"Đã xóa hóa đơn #{id} thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Không thể xóa hóa đơn {id}: {errorContent}");
                    TempData["ErrorMessage"] = $"Không thể xóa hóa đơn. Lỗi: {errorContent}";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xóa hóa đơn: {ex.Message}");
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }

    /// <summary>
    /// ViewModel cho nhân viên (để hiển thị trong dropdown)
    /// </summary>
    public class StaffViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}

