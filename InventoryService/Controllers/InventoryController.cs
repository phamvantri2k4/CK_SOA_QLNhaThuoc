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
        private readonly InventoryDbContext _context;

        public InventoryController(InventoryDbContext context)
        {
            _context = context;
        }

        // ✅ Nhập kho (SupplierService gọi)
        [HttpPost("import")]
        public async Task<IActionResult> ImportStock([FromBody] ImportStockRequest request)
        {
            if (request.DrugId <= 0)
            {
                return BadRequest("DrugId không hợp lệ");
            }
            if (request.Quantity <= 0)
            {
                return BadRequest("Quantity phải > 0");
            }

            var item = await _context.InventoryItems
                .FirstOrDefaultAsync(i =>
                    i.DrugId == request.DrugId &&
                    i.ExpiryDate == request.ExpiryDate);

            if (item == null)
            {
                item = new InventoryItem
                {
                    DrugId = request.DrugId,
                    Quantity = request.Quantity,
                    ExpiryDate = request.ExpiryDate
                };
                _context.InventoryItems.Add(item);
            }
            else
            {
                item.Quantity += request.Quantity;
            }

            _context.StockTransactions.Add(new StockTransaction
            {
                DrugId = request.DrugId,
                Quantity = request.Quantity,
                Type = "IMPORT",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return Ok("Nhập kho thành công");
        }

        // ✅ Xuất kho (SaleService gọi)
        [HttpPost("export")]
        public async Task<IActionResult> ExportStock([FromBody] ExportStockRequest request)
        {
            if (request.DrugId <= 0)
            {
                return BadRequest("DrugId không hợp lệ");
            }
            if (request.Quantity <= 0)
            {
                return BadRequest("Quantity phải > 0");
            }

            var drugId = request.DrugId;
            var quantity = request.Quantity;

            var items = await _context.InventoryItems
                .Where(i => i.DrugId == drugId && i.Quantity > 0)
                .OrderBy(i => i.ExpiryDate)
                .ToListAsync();

            int remain = quantity;

            foreach (var item in items)
            {
                if (remain == 0) break;

                var used = Math.Min(item.Quantity, remain);
                item.Quantity -= used;
                remain -= used;
            }

            if (remain > 0)
                return BadRequest("Không đủ tồn kho");

            _context.StockTransactions.Add(new StockTransaction
            {
                DrugId = drugId,
                Quantity = -quantity,
                Type = "EXPORT",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return Ok("Xuất kho thành công");
        }

        // ✅ Cảnh báo sắp hết hạn
        [HttpGet("expiry-warning")]
        public IActionResult ExpiryWarning()
        {
            var warning = _context.InventoryItems
                .Where(i => i.ExpiryDate <= DateTime.Now.AddDays(30))
                .ToList();

            return Ok(warning);
        }

        // ✅ Tồn kho hiện tại
        [HttpGet("status")]
        public IActionResult InventoryStatus()
        {
            return Ok(_context.InventoryItems.ToList());
        }
    }
}
