using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaleService.Data;
using SaleService.Model;
using Shared.Helpers;
using Shared.Services;
using System.Net.Http.Json;

using Microsoft.Extensions.Caching.Memory;

namespace SaleService.Controllers
{
    [ApiController]
    [Route("api/sales")]
    public class SalesController : ControllerBase
    {
        private readonly SaleDbContext _db;
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<SalesController> _logger;
        private readonly IMemoryCache _cache;
        private readonly ConsulServiceDiscovery _consulDiscovery;

        public SalesController(
            SaleDbContext db,
            IHttpClientFactory factory,
            ILogger<SalesController> logger,
            IMemoryCache cache,
            ConsulServiceDiscovery consulDiscovery)
        {
            _db = db;
            _factory = factory;
            _logger = logger;
            _cache = cache;
            _consulDiscovery = consulDiscovery;
        }

        // ...

        private async Task<string> GetServiceUrl(string serviceName)
        {
            return await _cache.GetOrCreateAsync(serviceName, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5); // Cache 5 phút
                return await _consulDiscovery.GetServiceUrlAsync(serviceName);
            }) ?? "";
        }

        /* ================= GET ================= */

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll(int? staffId = null)
        {
            var query = _db.SaleInvoices
                .Include(x => x.Details)
                .AsQueryable();

            if (staffId.HasValue && staffId.Value > 0)
            {
                query = query.Where(x => x.StaffId == staffId.Value);
            }

            var invoices = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            // PERFORMANCE: Không gọi external services cho danh sách
            var result = invoices.Select(inv => new SaleInvoiceResponse
            {
                Id = inv.Id,
                CreatedAt = inv.CreatedAt,
                StaffId = inv.StaffId,
                StaffName = $"NV-{inv.StaffId}", // Không gọi AuthService
                CustomerId = inv.CustomerId,
                CustomerName = inv.CustomerId.HasValue ? $"KH-{inv.CustomerId.Value}" : "Khách vãng lai",
                TotalAmount = inv.TotalAmount,
                PaymentStatus = inv.PaymentStatus,
                PaidAt = inv.PaidAt,
                Items = inv.Details.Select(d => new SaleItemResponse
                {
                    DrugId = d.DrugId,
                    DrugName = $"Thuốc-{d.DrugId}", // Không gọi DrugService
                    UnitType = d.UnitType,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    LineTotal = d.Quantity * d.UnitPrice
                }).ToList()
            }).ToList();

            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Staff,Owner")]
        public async Task<IActionResult> Get(int id)
        {
            var invoice = await _db.SaleInvoices
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == id);

            return invoice == null
                ? NotFound("Invoice not found")
                : Ok(await BuildResponse(invoice)); // CHỈ chi tiết mới gọi services
        }

        /* ================= CREATE ================= */

        [HttpPost]
        [Authorize(Roles = "Staff,Owner")]
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
                var price = unit == "box" ? drug.BoxPrice : drug.SellPricePerPill;

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
        [Authorize(Roles = "Staff,Owner")]
        public async Task<IActionResult> Pay(int id)
        {
            var invoice = await _db.SaleInvoices
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice == null) return NotFound();
            if (invoice.PaymentStatus == "Paid")
                return BadRequest("Already paid");

            var inventoryUrl = await GetServiceUrl("InventoryService");
            var client = await CreateServiceClient();

            foreach (var d in invoice.Details)
            {
                var qty = d.Quantity;
                var unitType = d.UnitType;

                // Nếu bán BOX → export BOX từ kho
                // Nếu bán PILL → export PILL từ kho
                // KHÔNG convert box → pill nữa!

                var resp = await client.PostAsJsonAsync(
                    $"{inventoryUrl}/api/inventory/export",
                    new { drugId = d.DrugId, quantity = qty, unitType = unitType });

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
            // 1. Prepare Tasks for fetching Drugs
            var drugTasks = inv.Details.Select(async d =>
            {
                try
                {
                    var drug = await GetDrug(d.DrugId);
                    return new SaleItemResponse
                    {
                        DrugId = d.DrugId,
                        DrugName = drug?.Name ?? $"Thuốc-{d.DrugId}",
                        UnitType = d.UnitType,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        LineTotal = d.Quantity * d.UnitPrice
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get drug {d.DrugId}: {ex.Message}");
                    return new SaleItemResponse
                    {
                        DrugId = d.DrugId,
                        DrugName = $"Thuốc-{d.DrugId}",
                        UnitType = d.UnitType,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        LineTotal = d.Quantity * d.UnitPrice
                    };
                }
            }).ToList();

            // 2. Prepare Tasks for Staff
            var staffTask = GetUser(inv.StaffId);

            // 3. Prepare Task for Customer (if fetching needed)
            Task<CustomerDto?>? customerTask = null;
            if (inv.CustomerId.HasValue)
            {
                customerTask = GetCustomer(inv.CustomerId.Value);
            }

            // 4. WAIT ALL PARALLEL (Chờ tất cả chạy song song)
            await Task.WhenAll(drugTasks);
            UserDto? staff = null;
            try { staff = await staffTask; } catch { }
            
            CustomerDto? customer = null;
            if (customerTask != null) 
            {
                 try { customer = await customerTask; } catch { }
            }

            // 5. Construct Result
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
                Items = drugTasks.Select(t => t.Result).ToList() // Safe to access .Result here
            };
        }



        private async Task<HttpClient> CreateServiceClient()
        {
            var client = _factory.CreateClient();
            var authUrl = await GetServiceUrl("AuthService");
            var token = await ServiceTokenHelper.GetServiceTokenAsync(client, authUrl);

            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        private async Task<DrugDto?> GetDrug(int id)
        {
            var client = await CreateServiceClient();
            var url = await GetServiceUrl("DrugService");
            return await client.GetFromJsonAsync<DrugDto>($"{url}/api/drugs/{id}");
        }

        private async Task<UserDto?> GetUser(int id)
        {
            var client = await CreateServiceClient();
            var url = await GetServiceUrl("AuthService");
            return await client.GetFromJsonAsync<UserDto>($"{url}/api/users/{id}");
        }

        private async Task<CustomerDto?> GetCustomer(int id)
        {
            var client = await CreateServiceClient();
            var url = await GetServiceUrl("CustomerService");
            return await client.GetFromJsonAsync<CustomerDto>($"{url}/api/customers/{id}");
        }

        private async Task<CustomerDto?> FindOrCreateCustomer(string name, string phone)
        {
            var client = await CreateServiceClient();
            var url = await GetServiceUrl("CustomerService");
            var response = await client.PostAsJsonAsync($"{url}/api/customers/find-or-create",
                new { name, phone });
            return await response.Content.ReadFromJsonAsync<CustomerDto>();
        }
    }
}
