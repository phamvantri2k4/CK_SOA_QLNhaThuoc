using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaleService.Data;
using SaleService.Model;
using Shared.Helpers;
using System.Net.Http.Json;

namespace SaleService.Controllers
{
    [ApiController]
    [Route("api/sales")]
    public class SalesController : ControllerBase
    {
        private readonly SaleDbContext _db;
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _config;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            SaleDbContext db,
            IHttpClientFactory factory,
            IConfiguration config,
            ILogger<SalesController> logger)
        {
            _db = db;
            _factory = factory;
            _config = config;
            _logger = logger;
        }

        /* ================= GET ================= */

        [HttpGet]
        [Authorize] // Allow authenticated requests including service tokens
        public async Task<IActionResult> GetAll()
        {
            var invoices = await _db.SaleInvoices
                .Include(x => x.Details)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var result = new List<SaleInvoiceResponse>();

            foreach (var inv in invoices)
                result.Add(await BuildResponse(inv));

            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Staff,Owner")] // Allow both Staff and Owner
        public async Task<IActionResult> Get(int id)
        {
            var invoice = await _db.SaleInvoices
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == id);

            return invoice == null
                ? NotFound("Invoice not found")
                : Ok(await BuildResponse(invoice));
        }

        /* ================= CREATE ================= */

        [HttpPost]
        [Authorize(Roles = "Staff,Owner")] // Allow both Staff and Owner to create invoices
        public async Task<IActionResult> Create(CreateSaleRequest req)
        {
            if (req.Items == null || req.Items.Count == 0)
                return BadRequest("Invoice must have items");

            var invoice = new SaleInvoice
            {
                CreatedAt = DateTime.Now,
                StaffId = req.StaffId > 0 ? req.StaffId : 1
            };

            if (!string.IsNullOrWhiteSpace(req.CustomerName) &&
                !string.IsNullOrWhiteSpace(req.CustomerPhone))
            {
                var c = await FindOrCreateCustomer(req.CustomerName, req.CustomerPhone);
                invoice.CustomerId = c?.Id;
            }

            foreach (var i in req.Items)
            {
                var drug = await GetDrug(i.DrugId);
                if (drug == null) return BadRequest($"Drug {i.DrugId} not found");

                var unit = string.IsNullOrWhiteSpace(i.UnitType) ? "pill" : i.UnitType;
                var price = unit == "box" ? drug.BoxPrice : drug.SellPrice;

                invoice.Details.Add(new SaleInvoiceDetail
                {
                    DrugId = i.DrugId,
                    UnitType = unit,
                    Quantity = i.Quantity,
                    UnitPrice = price
                });

                invoice.TotalAmount += i.Quantity * price;
            }

            _db.SaleInvoices.Add(invoice);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = invoice.Id }, await BuildResponse(invoice));
        }

        /* ================= PAY ================= */

        [HttpPut("{id}/pay")]
        [Authorize(Roles = "Staff,Owner")] // Allow both Staff and Owner to mark as paid
        public async Task<IActionResult> Pay(int id)
        {
            var invoice = await _db.SaleInvoices
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice == null) return NotFound();
            if (invoice.PaymentStatus == "Paid")
                return BadRequest("Already paid");

            var inventoryUrl = GetUrl("InventoryService");
            var client = CreateServiceClient();

            foreach (var d in invoice.Details)
            {
                var qty = d.Quantity;
                if (d.UnitType == "box")
                {
                    var drug = await GetDrug(d.DrugId);
                    qty *= drug?.PackSize ?? 1;
                }

                var resp = await client.PostAsJsonAsync(
                    $"{inventoryUrl}/api/inventory/export",
                    new { drugId = d.DrugId, quantity = qty });

                if (!resp.IsSuccessStatusCode)
                    return BadRequest("Not enough stock");
            }

            invoice.PaymentStatus = "Paid";
            invoice.PaidAt = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok("Paid");
        }

        /* ================= DELETE ================= */

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Delete(int id)
        {
            var invoice = await _db.SaleInvoices
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice == null) return NotFound();

            _db.SaleInvoiceDetails.RemoveRange(invoice.Details);
            _db.SaleInvoices.Remove(invoice);
            await _db.SaveChangesAsync();

            return Ok("Deleted");
        }

        /* ================= HELPERS ================= */

        private async Task<SaleInvoiceResponse> BuildResponse(SaleInvoice inv)
        {
            var items = new List<SaleItemResponse>();

            foreach (var d in inv.Details)
            {
                try
                {
                    var drug = await GetDrug(d.DrugId);
                    items.Add(new SaleItemResponse
                    {
                        DrugId = d.DrugId,
                        DrugName = drug?.Name ?? $"Thuốc-{d.DrugId}",
                        UnitType = d.UnitType,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        LineTotal = d.Quantity * d.UnitPrice
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get drug {d.DrugId}: {ex.Message}");
                    items.Add(new SaleItemResponse
                    {
                        DrugId = d.DrugId,
                        DrugName = $"Thuốc-{d.DrugId}",
                        UnitType = d.UnitType,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        LineTotal = d.Quantity * d.UnitPrice
                    });
                }
            }

            UserDto? staff = null;
            try
            {
                staff = await GetUser(inv.StaffId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get user {inv.StaffId}: {ex.Message}");
            }

            CustomerDto? customer = null;
            if (inv.CustomerId.HasValue)
            {
                try
                {
                    customer = await GetCustomer(inv.CustomerId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get customer {inv.CustomerId}: {ex.Message}");
                }
            }

            return new SaleInvoiceResponse
            {
                Id = inv.Id,
                CreatedAt = inv.CreatedAt,
                StaffId = inv.StaffId,
                StaffName = staff?.FullName ?? $"NV-{inv.StaffId}",
                CustomerId = inv.CustomerId,
                CustomerName = customer?.Name ?? "Khách vãng lai",
                TotalAmount = inv.TotalAmount,
                PaymentStatus = inv.PaymentStatus,
                PaidAt = inv.PaidAt,
                Items = items
            };
        }

        private HttpClient CreateServiceClient()
        {
            var client = _factory.CreateClient();
            var token = ServiceTokenHelper
                .GetServiceTokenAsync(client, GetUrl("AuthService")).Result;

            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        private string GetUrl(string key)
            => (_config[$"{key}:BaseUrl"] ?? "").TrimEnd('/');

        private async Task<DrugDto?> GetDrug(int id)
            => await CreateServiceClient()
                .GetFromJsonAsync<DrugDto>($"{GetUrl("DrugService")}/api/drugs/{id}");

        private async Task<UserDto?> GetUser(int id)
            => await CreateServiceClient()
                .GetFromJsonAsync<UserDto>($"{GetUrl("AuthService")}/api/users/{id}");

        private async Task<CustomerDto?> GetCustomer(int id)
            => await CreateServiceClient()
                .GetFromJsonAsync<CustomerDto>($"{GetUrl("CustomerService")}/api/customers/{id}");

        private async Task<CustomerDto?> FindOrCreateCustomer(string name, string phone)
            => await CreateServiceClient()
                .PostAsJsonAsync($"{GetUrl("CustomerService")}/api/customers/find-or-create",
                    new { name, phone })
                .Result.Content.ReadFromJsonAsync<CustomerDto>();
    }
}
