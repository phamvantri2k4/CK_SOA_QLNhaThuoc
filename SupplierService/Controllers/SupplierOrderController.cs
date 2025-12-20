using Microsoft.AspNetCore.Mvc;
using SupplierService.Data;
using SupplierService.Models;
using SupplierService.Services;
using Microsoft.EntityFrameworkCore;

namespace SupplierService.Controllers
{
    [ApiController]
    [Route("api/supplier/orders")]
    public class SupplierOrderController : ControllerBase
    {
        private readonly SupplierDbContext _context;
        private readonly InventoryClient _inventoryClient;

        public SupplierOrderController(
            SupplierDbContext context,
            InventoryClient inventoryClient)
        {
            _context = context;
            _inventoryClient = inventoryClient;
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _context.PurchaseOrders
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            var details = await _context.PurchaseOrderDetails
                .Where(d => d.PurchaseOrderId == id)
                .ToListAsync();

            return Ok(new
            {
                id = order.Id,
                supplierId = order.SupplierId,
                createdAt = order.CreatedAt,
                details = details
            });
        }

        [HttpGet("latest-prices")]
        public async Task<IActionResult> GetLatestPrices([FromQuery] int[] drugIds)
        {
            if (drugIds == null || drugIds.Length == 0)
            {
                return Ok(new List<object>());
            }

            var candidates = await (
                from d in _context.PurchaseOrderDetails.AsNoTracking()
                join o in _context.PurchaseOrders.AsNoTracking() on d.PurchaseOrderId equals o.Id
                join s in _context.Suppliers.AsNoTracking() on o.SupplierId equals s.Id
                where drugIds.Contains(d.DrugId)
                orderby o.CreatedAt descending
                select new
                {
                    drugId = d.DrugId,
                    unitPrice = d.UnitPrice,
                    createdAt = o.CreatedAt,
                    supplierId = s.Id,
                    supplierName = s.Name
                }
            ).ToListAsync();

            var latest = candidates
                .GroupBy(x => x.drugId)
                .Select(g => g.First())
                .ToList();

            return Ok(latest);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreatePurchaseOrderRequest request)
        {
            if (request.SupplierId <= 0)
            {
                return BadRequest("SupplierId không hợp lệ");
            }

            if (request.Details == null || request.Details.Count == 0)
            {
                return BadRequest("Details không được rỗng");
            }

            var supplierExists = await _context.Suppliers.AnyAsync(s => s.Id == request.SupplierId);
            if (!supplierExists)
            {
                return BadRequest("Supplier không tồn tại");
            }

            if (request.Details.Any(d => d.DrugId <= 0 || d.Quantity <= 0 || d.UnitPrice < 0))
            {
                return BadRequest("Chi tiết đơn nhập không hợp lệ (DrugId/Quantity/UnitPrice)");
            }

            var order = new PurchaseOrder
            {
                SupplierId = request.SupplierId,
                CreatedAt = DateTime.Now
            };

            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var d in request.Details)
            {
                var detail = new PurchaseOrderDetail
                {
                    PurchaseOrderId = order.Id,
                    DrugId = d.DrugId,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    ExpiryDate = d.ExpiryDate
                };
                _context.PurchaseOrderDetails.Add(detail);

                var ok = await _inventoryClient.ImportToInventory(d.DrugId, d.Quantity, d.ExpiryDate);
                if (!ok)
                {
                    return StatusCode(502, "Tạo đơn nhập thành công nhưng cập nhật kho thất bại");
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Tạo đơn nhập và cập nhật kho thành công", id = order.Id });
        }
    }
}
