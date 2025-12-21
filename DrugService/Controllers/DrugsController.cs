using DrugService.Data;
using DrugService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugService.Controllers
{
    [ApiController]
    [Route("api/drugs")]
    [Authorize]
    public class DrugsController : ControllerBase
    {
        private readonly DrugDbContext _db;

        public DrugsController(DrugDbContext db)
        {
            _db = db;
        }

        // GET: api/drugs
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var drugs = await _db.Drugs.ToListAsync();
            return Ok(drugs);
        }

        // GET: api/drugs/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var drug = await _db.Drugs.FindAsync(id);
            if (drug == null) return NotFound("Drug not found");
            return Ok(drug);
        }

        // POST: api/drugs
        [HttpPost]
        public async Task<IActionResult> Create(Drug drug)
        {
            if (string.IsNullOrWhiteSpace(drug.Name))
                return BadRequest("Name is required");

            if (drug.SellPricePerPill < 0 || drug.BoxPrice < 0)
                return BadRequest("Price must be >= 0");

            drug.PackSize = drug.PackSize <= 0 ? 1 : drug.PackSize;

            _db.Drugs.Add(drug);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = drug.Id }, drug);
        }

        // PUT: api/drugs/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Drug drug)
        {
            if (id != drug.Id)
                return BadRequest("ID mismatch");

            var dbDrug = await _db.Drugs.FindAsync(id);
            if (dbDrug == null) return NotFound("Drug not found");

            if (string.IsNullOrWhiteSpace(drug.Name))
                return BadRequest("Name is required");

            dbDrug.Name = drug.Name;
            dbDrug.Code = drug.Code;
            dbDrug.Category = drug.Category;
            dbDrug.Unit = drug.Unit;
            dbDrug.PackSize = drug.PackSize <= 0 ? 1 : drug.PackSize;
            dbDrug.ImportPrice = drug.ImportPrice;
            dbDrug.SellPricePerPill = drug.SellPricePerPill;
            dbDrug.BoxPrice = drug.BoxPrice;
            dbDrug.ImageUrl = drug.ImageUrl;

            await _db.SaveChangesAsync();
            return Ok(dbDrug);
        }

        // DELETE: api/drugs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var drug = await _db.Drugs.FindAsync(id);
            if (drug == null) return NotFound("Drug not found");

            _db.Drugs.Remove(drug);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
