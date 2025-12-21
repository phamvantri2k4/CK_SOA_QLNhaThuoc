using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;
using System.Security.Claims;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "StaffOrOwner")]
    public class SalesController : BaseController
    {
        private readonly IServiceResolver _resolver;

        public SalesController(IServiceResolver resolver)
        {
            _resolver = resolver;
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> SaleClientAsync()
        {
            var url = await _resolver.GetRequiredAsync("SaleService");
            return (CreateAuthenticatedHttpClient(), url);
        }

        private async Task<List<DrugViewModel>> GetDrugsAsync()
        {
            var url = await _resolver.GetRequiredAsync("DrugService");
            return await CreateAuthenticatedHttpClient()
                .GetFromJsonAsync<List<DrugViewModel>>($"{url}/api/drugs") ?? new();
        }

        private async Task<List<StaffViewModel>> GetStaffAsync()
        {
            var url = await _resolver.GetRequiredAsync("AuthService");
            return await CreateAuthenticatedHttpClient()
                .GetFromJsonAsync<List<StaffViewModel>>($"{url}/api/users") ?? new();
        }

        /* ================= DANH SÁCH HÓA ĐƠN ================= */

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, int? staffId = null)
        {
            try
            {
                var (client, url) = await SaleClientAsync();

                // Try to get staff list, but don't fail if it errors (e.g., 403)
                try
                {
                    ViewBag.StaffList = await GetStaffAsync();
                }
                catch (Exception staffEx)
                {
                    // Staff user không có quyền xem danh sách nhân viên, skip
                    ViewBag.StaffList = new List<StaffViewModel>();
                    Console.WriteLine($"Cannot load staff list: {staffEx.Message}");
                }
                
                ViewBag.SelectedStaffId = staffId;

                var api = $"{url}/api/sales";
                if (staffId.HasValue) api += $"?staffId={staffId}";

                var invoices = await client.GetFromJsonAsync<List<SaleInvoiceDisplayViewModel>>(api) ?? new();

                invoices = invoices.OrderByDescending(x => x.CreatedAt).ToList();

                return View(PagedList<SaleInvoiceDisplayViewModel>
                    .Create(invoices, page, pageSize));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải danh sách hóa đơn: {ex.Message}";
                ViewBag.StaffList = new List<StaffViewModel>();
                return View(PagedList<SaleInvoiceDisplayViewModel>
                    .Create(new List<SaleInvoiceDisplayViewModel>(), page, pageSize));
            }
        }

        /* ================= LẬP HÓA ĐƠN (SHOP) ================= */

        public async Task<IActionResult> OrderCreate()
        {
            try
            {
                // Fetch drugs
                var drugs = await GetDrugsAsync();
                ViewBag.Drugs = drugs;

                // Fetch inventory stock
                var inventoryUrl = await _resolver.GetOptionalAsync("InventoryService");
                var stockByDrugId = new Dictionary<int, int>();
                
                if (!string.IsNullOrEmpty(inventoryUrl))
                {
                    try
                    {
                        var client = CreateAuthenticatedHttpClient();
                        var inventoryItems = await client.GetFromJsonAsync<List<InventoryItemViewModel>>(
                            $"{inventoryUrl}/api/inventory/status") ?? new();

                        stockByDrugId = inventoryItems
                            .GroupBy(i => i.DrugId)
                            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with empty stock
                        Console.WriteLine($"Error fetching inventory: {ex.Message}");
                    }
                }

                ViewBag.StockByDrugId = stockByDrugId;

                // Get ALL categories from API, not just categories with drugs
                List<string> categories = new();
                try
                {
                    var drugServiceUrl = await _resolver.GetRequiredAsync("DrugService");
                    var client = CreateAuthenticatedHttpClient();
                    var categoriesData = await client.GetFromJsonAsync<List<CategoryViewModel>>(
                        $"{drugServiceUrl}/api/categories") ?? new();
                    
                    categories = categoriesData
                        .Select(c => c.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .OrderBy(c => c)
                        .ToList();
                }
                catch (Exception ex)
                {
                    // Fallback: get from drugs if API fails
                    Console.WriteLine($"Error fetching categories from API: {ex.Message}");
                    categories = drugs
                        .Where(d => !string.IsNullOrWhiteSpace(d.Category))
                        .Select(d => d.Category)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToList();
                }
                
                ViewBag.Categories = categories;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải dữ liệu: {ex.Message}";
                ViewBag.Drugs = new List<DrugViewModel>();
                ViewBag.StockByDrugId = new Dictionary<int, int>();
                ViewBag.Categories = new List<string>();
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitOrder([FromBody] CreateSaleViewModel m)
        {
            try
            {
                if (m.Items == null || !m.Items.Any())
                    return BadRequest("Giỏ hàng trống");

                if (string.IsNullOrWhiteSpace(m.CustomerName))
                    return BadRequest("Chưa nhập tên khách hàng");

                var staffId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");

                var (client, url) = await SaleClientAsync();

                var req = new
                {
                    customerName = m.CustomerName,
                    customerPhone = m.CustomerPhone,
                    staffId,
                    items = m.Items.Select(i => new
                    {
                        i.DrugId,
                        i.UnitType,
                        i.Quantity
                    })
                };

                // Fix: Use correct endpoint /api/sales instead of /api/sales/create
                var resp = await client.PostAsJsonAsync($"{url}/api/sales", req);

                if (!resp.IsSuccessStatusCode)
                {
                    var errorMsg = await resp.Content.ReadAsStringAsync();
                    return BadRequest(errorMsg);
                }

                var invoice = await resp.Content
                    .ReadFromJsonAsync<SaleInvoiceDisplayViewModel>();

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return BadRequest($"Lỗi khi tạo đơn hàng: {ex.Message}");
            }
        }

        /* ================= CHI TIẾT HÓA ĐƠN ================= */

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var (client, url) = await SaleClientAsync();

                var invoice = await client
                    .GetFromJsonAsync<SaleInvoiceDisplayViewModel>($"{url}/api/sales/{id}");

                if (invoice == null) return NotFound();
                return View(invoice);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải thông tin hóa đơn: {ex.Message}";
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /* ================= THANH TOÁN ================= */

        [HttpPost]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var (client, url) = await SaleClientAsync();
            var resp = await client.PutAsync($"{url}/api/sales/{id}/pay", null);

            if (!resp.IsSuccessStatusCode)
                return BadRequest("Không thể thanh toán");

            return Ok(new { message = "Thanh toán thành công" });
        }
    }

    /* ================= VIEWMODEL PHỤ ================= */

    public class StaffViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
