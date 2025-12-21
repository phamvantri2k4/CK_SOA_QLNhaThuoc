using DrugService.Data;
using DrugService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugService.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly DrugDbContext _db;
    private readonly ILogger<CategoriesController> _log;

    public CategoriesController(DrugDbContext db, ILogger<CategoriesController> log)
    {
        _db = db;
        _log = log;
    }

    /* ===== GET ALL ===== */

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Categories.OrderBy(x => x.Name).ToListAsync());

    /* ===== GET BY ID ===== */

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        return c == null ? NotFound("Category not found") : Ok(c);
    }

    /* ===== CREATE ===== */

    [HttpPost]
    public async Task<IActionResult> Create(Category req)
    {
        var name = req?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required");

        if (await _db.Categories.AnyAsync(x => x.Name == name))
            return Conflict("Category already exists");

        var c = new Category { Name = name };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
    }

    /* ===== UPDATE ===== */

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Category req)
    {
        var name = req?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required");

        var c = await _db.Categories.FindAsync(id);
        if (c == null) return NotFound("Category not found");

        if (await _db.Categories.AnyAsync(x => x.Id != id && x.Name == name))
            return Conflict("Category already exists");

        c.Name = name;
        await _db.SaveChangesAsync();

        return Ok(c);
    }

    /* ===== DELETE ===== */

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c == null) return NotFound("Category not found");

        var used = await _db.Drugs.AnyAsync(d => d.Category == c.Name);
        if (used)
            return Conflict("Category is being used by drugs");

        _db.Categories.Remove(c);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
