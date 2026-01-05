using InventoryService.Data;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers
{
    [ApiController]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryDbContext _db;

        public InventoryController(InventoryDbContext db)
        {
            _db = db;
        }

        /* ================= IMPORT ================= */
        // SupplierService gọi

        [HttpPost("import")]
        public async Task<IActionResult> Import(ImportStockRequest req)
        {
            if (req.DrugId <= 0 || req.Quantity <= 0)
                return BadRequest("Invalid DrugId or Quantity");

            // Tìm item với CÙNG DrugId, ExpiryDate VÀ UnitType
            var item = await _db.InventoryItems.FirstOrDefaultAsync(x =>
                x.DrugId == req.DrugId &&
                x.ExpiryDate == req.ExpiryDate &&
                x.UnitType == req.UnitType);

            if (item == null)
            {
                item = new InventoryItem
                {
                    DrugId = req.DrugId,
                    Quantity = req.Quantity,
                    UnitType = req.UnitType ?? "box", // Mặc định nhập theo hộp
                    ExpiryDate = req.ExpiryDate
                };
                _db.InventoryItems.Add(item);
            }
            else
            {
                item.Quantity += req.Quantity;
            }

            _db.StockTransactions.Add(new StockTransaction
            {
                DrugId = req.DrugId,
                Quantity = req.Quantity,
                UnitType = req.UnitType ?? "box",
                Type = "IMPORT",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok($"Import success: {req.Quantity} {req.UnitType ?? "box"}(s)");
        }

        /* ================= EXPORT ================= */
        // SaleService gọi

        [HttpPost("export")]
        public async Task<IActionResult> Export(ExportStockRequest req)
        {
            if (req.DrugId <= 0 || req.Quantity <= 0)
                return BadRequest("Invalid DrugId or Quantity");

            var items = await _db.InventoryItems
                .Where(x => x.DrugId == req.DrugId && x.Quantity > 0)
                .OrderBy(x => x.ExpiryDate)
                .ToListAsync();

            int remain = req.Quantity;

            foreach (var i in items)
            {
                if (remain == 0) break;

                var used = Math.Min(i.Quantity, remain);
                i.Quantity -= used;
                remain -= used;
            }

            if (remain > 0)
                return BadRequest("Not enough stock");

            _db.StockTransactions.Add(new StockTransaction
            {
                DrugId = req.DrugId,
                Quantity = -req.Quantity,
                Type = "EXPORT",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok("Export success");
        }

        /* ================= STATUS ================= */

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            return Ok(await _db.InventoryItems.ToListAsync());
        }

        /* ================= EXPIRY WARNING ================= */

        [HttpGet("expiry-warning")]
        public async Task<IActionResult> ExpiryWarning()
        {
            var soon = DateTime.UtcNow.AddDays(30);

            var items = await _db.InventoryItems
                .Where(x => x.ExpiryDate <= soon)
                .ToListAsync();

            return Ok(items);
        }
    }
}
