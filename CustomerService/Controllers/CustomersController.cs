using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CustomerService.Data;
using CustomerService.Models;
using System.Net.Http.Json;

namespace CustomerService.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly CustomerDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<CustomersController> _log;

    public CustomersController(CustomerDbContext db, IHttpClientFactory http,
        IConfiguration cfg, ILogger<CustomersController> log)
    {
        _db = db; _http = http; _cfg = cfg; _log = log;
    }

    /* ===== CRUD ===== */

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Customers.ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
        => await _db.Customers.FindAsync(id) is Customer c ? Ok(c) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CustomerReq r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Phone))
            return BadRequest("Name & Phone required");

        if (await _db.Customers.AnyAsync(x => x.Phone == r.Phone.Trim()))
            return Conflict("Phone exists");

        var c = new Customer { Name = r.Name.Trim(), Phone = r.Phone.Trim() };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CustomerReq r)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();

        if (await _db.Customers.AnyAsync(x => x.Phone == r.Phone.Trim() && x.Id != id))
            return Conflict("Phone exists");

        c.Name = r.Name.Trim();
        c.Phone = r.Phone.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        _db.Customers.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /* ===== FIND OR CREATE ===== */

    [HttpPost("find-or-create")]
    public async Task<IActionResult> FindOrCreate(CustomerReq r)
    {
        var phone = r.Phone.Trim();
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Phone == phone);
        if (c != null)
        {
            if (c.Name != r.Name.Trim())
            {
                c.Name = r.Name.Trim();
                await _db.SaveChangesAsync();
            }
            return Ok(c);
        }

        c = new Customer { Name = r.Name.Trim(), Phone = phone };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
    }

    /* ===== PURCHASE HISTORY ===== */

    [HttpGet("{id}/purchase-history")]
    public async Task<IActionResult> History(int id)
    {
        if (!await _db.Customers.AnyAsync(x => x.Id == id))
            return NotFound("Customer not found");

        var data = await SaleHelper.GetHistory(_http, _cfg, _log, id);
        return Ok(data);
    }
}

/* ===== SALE SERVICE HELPER ===== */

static class SaleHelper
{
    static string? token;
    static DateTime exp;
    const string Key = "ServiceKey123!";

    public static async Task<List<HistoryItem>> GetHistory(
        IHttpClientFactory f, IConfiguration c, ILogger log, int cid)
    {
        try
        {
            var sale = c["SaleService:BaseUrl"];
            var auth = c["AuthService:BaseUrl"];
            if (sale == null || auth == null) return new();

            var client = f.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new("Bearer", await GetToken(client, auth));

            var r = await client.GetAsync($"{sale}/api/sales");
            if (!r.IsSuccessStatusCode) return new();

            var sales = await r.Content.ReadFromJsonAsync<List<SaleDto>>() ?? new();
            return sales.Where(x => x.CustomerId == cid)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new HistoryItem
                {
                    InvoiceId = x.Id,
                    CreatedAt = x.CreatedAt,
                    TotalAmount = x.TotalAmount,
                    ItemCount = x.Items?.Count ?? 0
                }).ToList();
        }
        catch (Exception e)
        {
            log.LogError(e.Message);
            return new();
        }
    }

    static async Task<string?> GetToken(HttpClient c, string auth)
    {
        if (!string.IsNullOrEmpty(token) && DateTime.UtcNow < exp) return token;
        var r = await c.PostAsJsonAsync($"{auth}/api/auth/service-token", new { ServiceKey = Key });
        if (!r.IsSuccessStatusCode) return null;
        var j = await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        token = j.GetProperty("token").GetString();
        exp = DateTime.UtcNow.AddHours(23);
        return token;
    }
}

/* ===== DTO ===== */

public record CustomerReq(string Name, string Phone);

class SaleDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public List<object>? Items { get; set; }
}

class HistoryItem
{
    public int InvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}
