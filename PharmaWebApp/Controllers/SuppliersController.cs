using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class SuppliersController : BaseController
    {
        private readonly ILogger<SuppliersController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public SuppliersController(ILogger<SuppliersController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var baseUrl = await _serviceResolver.GetRequiredAsync("SupplierService");
                var httpClient = CreateAuthenticatedHttpClient();

                var resp = await httpClient.GetAsync($"{baseUrl}/api/supplier/suppliers");
                if (!resp.IsSuccessStatusCode)
                {
                    ViewBag.ErrorMessage = "Không thể kết nối SupplierService.";
                    return View(new List<SupplierViewModel>());
                }

                var suppliers = await resp.Content.ReadFromJsonAsync<List<SupplierViewModel>>() ?? new();
                return View(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy suppliers: {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<SupplierViewModel>());
            }
        }

        public IActionResult Create()
        {
            return View(new CreateSupplierViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSupplierViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var baseUrl = await _serviceResolver.GetRequiredAsync("SupplierService");
                var httpClient = CreateAuthenticatedHttpClient();

                var payload = new { name = model.Name, phone = model.Phone, address = model.Address };
                var resp = await httpClient.PostAsJsonAsync($"{baseUrl}/api/supplier/suppliers", payload);

                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã tạo nhà cung cấp";
                    return RedirectToAction(nameof(Index));
                }

                var error = await resp.Content.ReadAsStringAsync();
                ModelState.AddModelError("", error);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi tạo supplier: {ex.Message}");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        public async Task<IActionResult> Orders()
        {
            try
            {
                var baseUrl = await _serviceResolver.GetRequiredAsync("SupplierService");
                var httpClient = CreateAuthenticatedHttpClient();

                var resp = await httpClient.GetAsync($"{baseUrl}/api/supplier/orders");
                if (!resp.IsSuccessStatusCode)
                {
                    ViewBag.ErrorMessage = "Không thể lấy danh sách đơn nhập.";
                    return View(new List<PurchaseOrderViewModel>());
                }

                var orders = await resp.Content.ReadFromJsonAsync<List<PurchaseOrderViewModel>>() ?? new();

                var suppliers = await GetSuppliersAsync();
                var supplierMap = suppliers.ToDictionary(s => s.Id, s => s);
                foreach (var o in orders)
                {
                    if (supplierMap.TryGetValue(o.SupplierId, out var s))
                    {
                        o.SupplierName = s.Name;
                    }
                }

                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi lấy orders: {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<PurchaseOrderViewModel>());
            }
        }

        public async Task<IActionResult> CreateOrder()
        {
            var suppliers = await GetSuppliersAsync();
            ViewBag.Suppliers = suppliers;

            var drugs = await GetDrugsAsync();
            ViewBag.Drugs = drugs;

            var vm = new CreatePurchaseOrderViewModel();
            vm.Details.Add(new CreatePurchaseOrderDetailViewModel());
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(CreatePurchaseOrderViewModel model)
        {
            var suppliers = await GetSuppliersAsync();
            ViewBag.Suppliers = suppliers;
            var drugs = await GetDrugsAsync();
            ViewBag.Drugs = drugs;

            if (model.SupplierId <= 0)
            {
                ModelState.AddModelError("SupplierId", "Vui lòng chọn nhà cung cấp");
            }

            model.Details = model.Details
                .Where(d => d.DrugId > 0 && d.Quantity > 0 && d.UnitPrice > 0)
                .ToList();

            if (model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng thêm ít nhất 1 thuốc và nhập đủ Số lượng + Đơn giá");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Suppliers = await GetSuppliersAsync();
                ViewBag.Drugs = await GetDrugsAsync();
                return View(model);
            }

            try
            {
                var baseUrl = await _serviceResolver.GetRequiredAsync("SupplierService");
                var httpClient = CreateAuthenticatedHttpClient();

                var payload = new
                {
                    supplierId = model.SupplierId,
                    details = model.Details.Select(d => new
                    {
                        drugId = d.DrugId,
                        quantity = d.Quantity,
                        unitPrice = d.UnitPrice,
                        expiryDate = d.ExpiryDate
                    }).ToList()
                };

                var resp = await httpClient.PostAsJsonAsync($"{baseUrl}/api/supplier/orders", payload);
                if (resp.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Tạo đơn nhập thành công";
                    return RedirectToAction(nameof(Orders));
                }

                var error = await resp.Content.ReadAsStringAsync();
                ModelState.AddModelError("", error);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi tạo đơn nhập: {ex.Message}");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            try
            {
                var baseUrl = await _serviceResolver.GetRequiredAsync("SupplierService");
                var httpClient = CreateAuthenticatedHttpClient();

                var resp = await httpClient.GetAsync($"{baseUrl}/api/supplier/orders/{id}");
                if (!resp.IsSuccessStatusCode)
                {
                    return NotFound();
                }

                var order = await resp.Content.ReadFromJsonAsync<PurchaseOrderDetailsResponseViewModel>();
                if (order == null) return NotFound();

                var suppliers = await GetSuppliersAsync();
                var supplierMap = suppliers.ToDictionary(s => s.Id, s => s);
                if (supplierMap.TryGetValue(order.SupplierId, out var supplier))
                {
                    order.SupplierName = supplier.Name;
                }

                var drugs = await GetDrugsAsync();
                var drugMap = drugs.ToDictionary(d => d.Id, d => d);
                foreach (var d in order.Details)
                {
                    if (drugMap.TryGetValue(d.DrugId, out var drug))
                    {
                        d.DrugName = drug.Name;
                    }
                }

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi order details: {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                return View();
            }
        }

        private async Task<List<SupplierViewModel>> GetSuppliersAsync()
        {
            try
            {
                var baseUrl = await _serviceResolver.GetRequiredAsync("SupplierService");
                var httpClient = CreateAuthenticatedHttpClient();

                var resp = await httpClient.GetAsync($"{baseUrl}/api/supplier/suppliers");
                if (!resp.IsSuccessStatusCode) return new();
                return await resp.Content.ReadFromJsonAsync<List<SupplierViewModel>>() ?? new();
            }
            catch
            {
                return new();
            }
        }

        private async Task<List<DrugViewModel>> GetDrugsAsync()
        {
            try
            {
                var drugServiceUrl = await _serviceResolver.GetRequiredAsync("DrugService");
                var httpClient = CreateAuthenticatedHttpClient();

                var resp = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs");
                if (!resp.IsSuccessStatusCode) return new();
                return await resp.Content.ReadFromJsonAsync<List<DrugViewModel>>() ?? new();
            }
            catch
            {
                return new();
            }
        }
    }
}
