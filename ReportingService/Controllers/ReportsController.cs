using Microsoft.AspNetCore.Mvc;
using ReportingService.Models;
using Shared.Helpers;
using System.Net.Http.Json;

namespace ReportingService.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IHttpClientFactory factory,
            ILogger<ReportsController> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /* ================= DAY ================= */

        [HttpGet("day")]
        public Task<IActionResult> Day(DateTime? date)
        {
            var d = (date ?? DateTime.Today).Date;
            return Build("day", d, d.AddDays(1), date: d);
        }

        /* ================= MONTH ================= */

        [HttpGet("month")]
        public Task<IActionResult> Month(int? year, int? month)
        {
            var now = DateTime.Today;
            var y = year ?? now.Year;
            var m = month ?? now.Month;

            var from = new DateTime(y, m, 1);
            return Build("month", from, from.AddMonths(1), y, m);
        }

        /* ================= YEAR ================= */

        [HttpGet("year")]
        public Task<IActionResult> Year(int? year)
        {
            var y = year ?? DateTime.Today.Year;
            var from = new DateTime(y, 1, 1);
            return Build("year", from, from.AddYears(1), y);
        }

        /* ================= CORE LOGIC ================= */

        private async Task<IActionResult> Build(
            string type,
            DateTime from,
            DateTime to,
            int? year = null,
            int? month = null,
            DateTime? date = null)
        {
            try
            {
                var client = _factory.CreateClient();

                // 🔐 Get AuthService URL from Consul and get service token
                var authUrl = await GetServiceUrl("AuthService");
                var token = await ServiceTokenHelper.GetServiceTokenAsync(client, authUrl);
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                // 📦 Get SaleService URL from Consul
                var saleUrl = await GetServiceUrl("SaleService");

                // 📄 Get invoices
                var invoices = await client
                    .GetFromJsonAsync<List<SaleInvoiceDisplayViewModel>>($"{saleUrl}/api/sales")
                    ?? new();

                var filtered = invoices
                    .Where(x => x.CreatedAt >= from && x.CreatedAt < to)
                    .ToList();

                var paid = filtered.Where(x => x.PaymentStatus == "Paid").ToList();

                var topDrugs = filtered
                    .SelectMany(i => i.Items)
                    .GroupBy(i => new { i.DrugId, i.DrugName })
                    .Select(g => new ReportTopDrugViewModel
                    {
                        DrugId = g.Key.DrugId,
                        DrugName = g.Key.DrugName,
                        Quantity = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.LineTotal)
                    })
                    .OrderByDescending(x => x.Revenue)
                    .Take(10)
                    .ToList();

                return Ok(new SalesReportViewModel
                {
                    ReportType = type,
                    From = from,
                    To = to,
                    InvoiceCount = filtered.Count,
                    PaidCount = paid.Count,
                    PendingCount = filtered.Count - paid.Count,
                    TotalRevenue = filtered.Sum(x => x.TotalAmount),
                    PaidRevenue = paid.Sum(x => x.TotalAmount),
                    TopDrugs = topDrugs,
                    Invoices = filtered,
                    SelectedYear = year,
                    SelectedMonth = month,
                    SelectedDate = date
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Report error ({type}): {ex.Message}");
                return StatusCode(500, $"Cannot build report: {ex.Message}");
            }
        }

        /* ================= SERVICE DISCOVERY (CONSUL) ================= */

        private async Task<string> GetServiceUrl(string serviceName)
        {
            var fallbackUrls = new Dictionary<string, string>
            {
                ["AuthService"] = "http://localhost:5004",
                ["SaleService"] = "http://localhost:5002"
            };

            try
            {
                using var consulClient = new Consul.ConsulClient(config =>
                {
                    config.Address = new Uri("http://localhost:8500");
                });

                var services = await consulClient.Health.Service(serviceName, null, true);
                var service = services.Response?.FirstOrDefault();

                if (service != null)
                {
                    return $"http://{service.Service.Address}:{service.Service.Port}";
                }

                return fallbackUrls.GetValueOrDefault(serviceName, "");
            }
            catch
            {
                return fallbackUrls.GetValueOrDefault(serviceName, "");
            }
        }
    }
}
