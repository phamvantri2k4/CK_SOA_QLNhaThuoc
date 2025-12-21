using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class InventoryController : BaseController
    {
        private readonly IServiceResolver _resolver;
        private readonly IConfiguration _config;

        public InventoryController(IServiceResolver resolver, IConfiguration config)
        {
            _resolver = resolver;
            _config = config;
        }

        /* ================= INDEX ================= */

        public async Task<IActionResult> Index()
        {
            try
            {
                var (client, inventoryUrl) = await GetClientAsync("InventoryService");

                // Lấy tồn kho
                var items = await client.GetFromJsonAsync<List<InventoryItemViewModel>>(
                    $"{inventoryUrl}/api/inventory/status") ?? new();

                // Lấy cảnh báo hết hạn
                var expiryWarnings = await client.GetFromJsonAsync<List<InventoryItemViewModel>>(
                    $"{inventoryUrl}/api/inventory/expiry-warning") ?? new();

                // Lấy thuốc
                var drugs = await GetDrugsAsync();
                var drugMap = drugs.ToDictionary(d => d.Id);

                // Lấy giá nhập mới nhất
                var latestImports = await GetLatestImportPricesAsync(items.Select(i => i.DrugId).Distinct().ToList());

                // Gán thông tin thuốc + giá nhập
                FillDrugInfo(items, drugMap, latestImports);
                FillDrugInfo(expiryWarnings, drugMap, latestImports);

                // Cảnh báo sắp hết hàng
                var threshold = _config.GetValue<int>("Inventory:LowStockThreshold", 20);
                ViewBag.LowStockThreshold = threshold;
                ViewBag.LowStockWarnings = items
                    .GroupBy(i => i.DrugId)
                    .Select(g => new InventoryLowStockWarningViewModel
                    {
                        DrugId = g.Key,
                        DrugName = drugMap.ContainsKey(g.Key) ? drugMap[g.Key].Name : null,
                        TotalQuantity = g.Sum(x => x.Quantity),
                        PackSize = drugMap.ContainsKey(g.Key)
                            ? Math.Max(1, drugMap[g.Key].PackSize)
                            : 1
                    })
                    .Where(x => x.TotalQuantity <= threshold)
                    .OrderBy(x => x.TotalQuantity)
                    .ToList();

                ViewBag.ExpiryWarnings = expiryWarnings;
                return View(items);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải thông tin kho: {ex.Message}";
                ViewBag.LowStockWarnings = new List<InventoryLowStockWarningViewModel>();
                ViewBag.ExpiryWarnings = new List<InventoryItemViewModel>();
                return View(new List<InventoryItemViewModel>());
            }
        }

        /* ================= HÀM PHỤ ================= */

        private async Task<(HttpClient client, string url)> GetClientAsync(string serviceName)
        {
            var url = await _resolver.GetRequiredAsync(serviceName);
            return (CreateAuthenticatedHttpClient(), url);
        }

        private async Task<List<DrugViewModel>> GetDrugsAsync()
        {
            var (client, url) = await GetClientAsync("DrugService");
            return await client.GetFromJsonAsync<List<DrugViewModel>>(
                $"{url}/api/drugs") ?? new();
        }

        private async Task<Dictionary<int, LatestImportPriceViewModel>> GetLatestImportPricesAsync(List<int> drugIds)
        {
            if (!drugIds.Any()) return new();

            var (client, url) = await GetClientAsync("SupplierService");
            var qs = string.Join("&", drugIds.Select(id => $"drugIds={id}"));

            var list = await client.GetFromJsonAsync<List<LatestImportPriceViewModel>>(
                $"{url}/api/supplier/orders/latest-prices?{qs}") ?? new();

            return list.GroupBy(x => x.DrugId)
                       .ToDictionary(g => g.Key, g => g.First());
        }

        private void FillDrugInfo(
            List<InventoryItemViewModel> items,
            Dictionary<int, DrugViewModel> drugMap,
            Dictionary<int, LatestImportPriceViewModel> importMap)
        {
            foreach (var i in items)
            {
                if (drugMap.TryGetValue(i.DrugId, out var drug))
                {
                    i.DrugName = drug.Name;
                    i.PackSize = Math.Max(1, drug.PackSize);
                    i.SellPricePerPill = drug.SellPricePerPill;
                    i.BoxPrice = drug.BoxPrice;
                }

                if (importMap.TryGetValue(i.DrugId, out var imp))
                {
                    i.ImportPrice = imp.UnitPrice;
                    i.ImportSupplierName = imp.SupplierName;
                    i.ImportCreatedAt = imp.CreatedAt;
                }
            }
        }
    }
}
