using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierService.Data;
using SupplierService.Models;

namespace SupplierService.Controllers
{
    [ApiController]
    [Route("api/supplier/suppliers")]
    public class SuppliersController : ControllerBase
    {
        private readonly SupplierDbContext _context;

        public SuppliersController(SupplierDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
            return Ok(suppliers);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();
            return Ok(supplier);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Supplier supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier.Name)) return BadRequest("Name không được rỗng");
            if (string.IsNullOrWhiteSpace(supplier.Phone)) return BadRequest("Phone không được rỗng");
            if (string.IsNullOrWhiteSpace(supplier.Address)) return BadRequest("Address không được rỗng");

            supplier.Id = 0;
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return Ok(supplier);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Supplier supplier)
        {
            if (id <= 0) return BadRequest("Id không hợp lệ");
            if (string.IsNullOrWhiteSpace(supplier.Name)) return BadRequest("Name không được rỗng");
            if (string.IsNullOrWhiteSpace(supplier.Phone)) return BadRequest("Phone không được rỗng");
            if (string.IsNullOrWhiteSpace(supplier.Address)) return BadRequest("Address không được rỗng");

            var existing = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null) return NotFound();

            existing.Name = supplier.Name;
            existing.Phone = supplier.Phone;
            existing.Address = supplier.Address;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa supplier", id });
        }
    }
}
