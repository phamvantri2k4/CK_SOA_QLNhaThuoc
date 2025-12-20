using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class InventoryController : BaseController
    {
        private readonly ILogger<InventoryController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public InventoryController(ILogger<InventoryController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var inventoryUrl = await _serviceResolver.GetRequiredAsync("InventoryService");
                var httpClient = CreateAuthenticatedHttpClient();

                var statusResp = await httpClient.GetAsync($"{inventoryUrl}/api/inventory/status");
                var warningResp = await httpClient.GetAsync($"{inventoryUrl}/api/inventory/expiry-warning");

                if (!statusResp.IsSuccessStatusCode)
                {
                    ViewBag.ErrorMessage = "Không thể kết nối InventoryService. Vui lòng kiểm tra service đã chạy chưa.";
                    return View(new List<InventoryItemViewModel>());
                }

                var items = await statusResp.Content.ReadFromJsonAsync<List<InventoryItemViewModel>>() ?? new();
                var warnings = warningResp.IsSuccessStatusCode
                    ? (await warningResp.Content.ReadFromJsonAsync<List<InventoryItemViewModel>>() ?? new())
                    : new List<InventoryItemViewModel>();

                var drugs = await GetDrugsAsync();
                var drugMap = drugs.ToDictionary(d => d.Id, d => d);

                var distinctDrugIds = items.Select(i => i.DrugId).Distinct().ToList();
                var latestImportMap = await GetLatestImportPricesAsync(distinctDrugIds);

                foreach (var item in items)
                {
                    if (drugMap.TryGetValue(item.DrugId, out var drug))
                    {
                        item.DrugName = drug.Name;
                        item.PackSize = drug.PackSize <= 0 ? 1 : drug.PackSize;
                        item.SellPrice = drug.SellPrice;
                        item.BoxPrice = drug.BoxPrice;
                    }

                    if (latestImportMap.TryGetValue(item.DrugId, out var latest))
                    {
                        item.ImportPrice = latest.UnitPrice;
                        item.ImportSupplierName = latest.SupplierName;
                        item.ImportCreatedAt = latest.CreatedAt;
                    }
                }

                foreach (var w in warnings)
                {
                    if (drugMap.TryGetValue(w.DrugId, out var drug))
                    {
                        w.DrugName = drug.Name;
                        w.PackSize = drug.PackSize <= 0 ? 1 : drug.PackSize;
                        w.SellPrice = drug.SellPrice;
                        w.BoxPrice = drug.BoxPrice;
                    }

                    if (latestImportMap.TryGetValue(w.DrugId, out var latest))
                    {
                        w.ImportPrice = latest.UnitPrice;
                        w.ImportSupplierName = latest.SupplierName;
                        w.ImportCreatedAt = latest.CreatedAt;
                    }
                }

                var threshold = 20;
                var thresholdText = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Inventory:LowStockThreshold"];
                if (!string.IsNullOrWhiteSpace(thresholdText) && int.TryParse(thresholdText, out var parsed))
                {
                    threshold = parsed;
                }

                var lowStock = items
                    .GroupBy(i => i.DrugId)
                    .Select(g => new InventoryLowStockWarningViewModel
                    {
                        DrugId = g.Key,
                        DrugName = drugMap.TryGetValue(g.Key, out var drug) ? drug.Name : null,
                        TotalQuantity = g.Sum(x => x.Quantity),
                        PackSize = drugMap.TryGetValue(g.Key, out var drug2) ? (drug2.PackSize <= 0 ? 1 : drug2.PackSize) : 1
                    })
                    .Where(x => x.TotalQuantity <= threshold)
                    .OrderBy(x => x.TotalQuantity)
                    .ToList();

                ViewBag.LowStockThreshold = threshold;
                ViewBag.LowStockWarnings = lowStock;

                ViewBag.ExpiryWarnings = warnings;
                return View(items);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy tồn kho: {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<InventoryItemViewModel>());
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

        private async Task<Dictionary<int, LatestImportPriceViewModel>> GetLatestImportPricesAsync(List<int> drugIds)
        {
            if (drugIds.Count == 0) return new();

            try
            {
                var supplierUrl = await _serviceResolver.GetRequiredAsync("SupplierService");
                var httpClient = CreateAuthenticatedHttpClient();

                var qs = string.Join("&", drugIds.Select(id => $"drugIds={id}"));
                var resp = await httpClient.GetAsync($"{supplierUrl}/api/supplier/orders/latest-prices?{qs}");
                if (!resp.IsSuccessStatusCode) return new();

                var list = await resp.Content.ReadFromJsonAsync<List<LatestImportPriceViewModel>>() ?? new();
                return list
                    .GroupBy(x => x.DrugId)
                    .ToDictionary(g => g.Key, g => g.First());
            }
            catch
            {
                return new();
            }
        }
    }
}
