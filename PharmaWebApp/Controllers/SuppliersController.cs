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
        private readonly ServiceUrlHelper _serviceUrl;

        public SuppliersController(ServiceUrlHelper serviceUrl)
        {
            _serviceUrl = serviceUrl;
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> SupplierClientAsync()
        {
            var url = await _serviceUrl.GetSupplierServiceUrlAsync();
            return (CreateAuthenticatedHttpClient(), url);
        }

        private async Task<List<SupplierViewModel>> GetSuppliersAsync()
        {
            var (client, url) = await SupplierClientAsync();
            return await client.GetFromJsonAsync<List<SupplierViewModel>>(
                $"{url}/api/supplier/suppliers") ?? new();
        }

        private async Task<List<DrugViewModel>> GetDrugsAsync()
        {
            var url = await _serviceUrl.GetDrugServiceUrlAsync();
            return await CreateAuthenticatedHttpClient()
                .GetFromJsonAsync<List<DrugViewModel>>($"{url}/api/drugs") ?? new();
        }

        /* ================= SUPPLIER CRUD ================= */

        public async Task<IActionResult> Index()
        {
            return View(await GetSuppliersAsync());
        }

        public IActionResult Create()
        {
            return View(new CreateSupplierViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSupplierViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var (client, url) = await SupplierClientAsync();

            await client.PostAsJsonAsync($"{url}/api/supplier/suppliers", new
            {
                m.Name,
                m.Phone,
                m.Address
            });

            TempData["SuccessMessage"] = "Đã tạo nhà cung cấp";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var (client, url) = await SupplierClientAsync();
            var supplier = await client.GetFromJsonAsync<SupplierViewModel>(
                $"{url}/api/supplier/suppliers/{id}");

            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SupplierViewModel m)
        {
            if (id != m.Id || !ModelState.IsValid) return View(m);

            var (client, url) = await SupplierClientAsync();

            await client.PutAsJsonAsync($"{url}/api/supplier/suppliers/{id}", new
            {
                m.Id,
                m.Name,
                m.Phone,
                m.Address
            });

            TempData["SuccessMessage"] = "Đã cập nhật nhà cung cấp";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var (client, url) = await SupplierClientAsync();
            var supplier = await client.GetFromJsonAsync<SupplierViewModel>(
                $"{url}/api/supplier/suppliers/{id}");

            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var (client, url) = await SupplierClientAsync();
            await client.DeleteAsync($"{url}/api/supplier/suppliers/{id}");

            TempData["SuccessMessage"] = "Đã xóa nhà cung cấp";
            return RedirectToAction(nameof(Index));
        }

        /* ================= ĐƠN NHẬP ================= */

        public async Task<IActionResult> Orders()
        {
            var (client, url) = await SupplierClientAsync();
            var orders = await client.GetFromJsonAsync<List<PurchaseOrderViewModel>>(
                $"{url}/api/supplier/orders") ?? new();

            var suppliers = await GetSuppliersAsync();
            var map = suppliers.ToDictionary(s => s.Id, s => s.Name);

            orders.ForEach(o =>
            {
                if (map.ContainsKey(o.SupplierId))
                    o.SupplierName = map[o.SupplierId];
            });

            return View(orders);
        }

        public async Task<IActionResult> CreateOrder()
        {
            ViewBag.Suppliers = await GetSuppliersAsync();
            ViewBag.Drugs = await GetDrugsAsync();

            var vm = new CreatePurchaseOrderViewModel();
            vm.Details.Add(new CreatePurchaseOrderDetailViewModel());

            return View("CreateOrder_New", vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(CreatePurchaseOrderViewModel m)
        {
            ViewBag.Suppliers = await GetSuppliersAsync();
            var drugs = await GetDrugsAsync();
            ViewBag.Drugs = drugs;

            m.Details = m.Details
                .Where(d => d.DrugId > 0 && d.Quantity > 0 && d.UnitPrice > 0)
                .ToList();

            if (m.SupplierId <= 0 || m.Details.Count == 0)
            {
                ModelState.AddModelError("", "Chọn nhà cung cấp và nhập ít nhất 1 thuốc");
                return View("CreateOrder_New", m);
            }

            var (client, url) = await SupplierClientAsync();

            // Build details với convert hộp sang viên nếu cần
            var details = m.Details.Select(d =>
            {
                var drug = drugs.FirstOrDefault(dr => dr.Id == d.DrugId);
                var packSize = drug?.PackSize ?? 1;
                if (packSize <= 0) packSize = 1;

                var quantityInPills = d.Quantity;
                var unitPricePerPill = d.UnitPrice;

                // Nếu nhập theo hộp, convert số lượng sang viên và chia giá theo viên
                if (d.UnitType == "box")
                {
                    quantityInPills = d.Quantity * packSize;
                    unitPricePerPill = d.UnitPrice / (decimal)packSize;
                }

                return new
                {
                    d.DrugId,
                    Quantity = quantityInPills,  // Luôn lưu theo viên vào kho
                    UnitPrice = unitPricePerPill, // Lưu giá theo viên để nhân ra tiền đúng
                    d.ExpiryDate
                };
            });

            await client.PostAsJsonAsync($"{url}/api/supplier/orders", new
            {
                supplierId = m.SupplierId,
                details
            });

            TempData["SuccessMessage"] = "Tạo đơn nhập thành công";
            return RedirectToAction(nameof(Orders));
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var (client, url) = await SupplierClientAsync();
            var order = await client.GetFromJsonAsync<PurchaseOrderDetailsResponseViewModel>(
                $"{url}/api/supplier/orders/{id}");

            if (order == null) return NotFound();

            var suppliers = await GetSuppliersAsync();
            var drugs = await GetDrugsAsync();

            order.SupplierName = suppliers
                .FirstOrDefault(s => s.Id == order.SupplierId)?.Name;

            var drugMap = drugs.ToDictionary(d => d.Id, d => d.Name);
            order.Details.ForEach(d =>
            {
                if (drugMap.ContainsKey(d.DrugId))
                    d.DrugName = drugMap[d.DrugId];
            });

            return View(order);
        }
    }
}
