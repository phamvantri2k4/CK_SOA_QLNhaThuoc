using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class ReportsController : BaseController
    {
        private readonly ILogger<ReportsController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public ReportsController(ILogger<ReportsController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        [HttpGet]
        public Task<IActionResult> Day(DateTime? date)
        {
            var d = date?.Date ?? DateTime.Today;
            var from = d;
            var to = d.AddDays(1);
            return BuildReport("day", from, to, selectedDate: d);
        }

        [HttpGet]
        public Task<IActionResult> Month(int? year, int? month)
        {
            var now = DateTime.Today;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            var from = new DateTime(y, m, 1);
            var to = from.AddMonths(1);
            return BuildReport("month", from, to, selectedYear: y, selectedMonth: m);
        }

        [HttpGet]
        public Task<IActionResult> Year(int? year)
        {
            var now = DateTime.Today;
            var y = year ?? now.Year;
            var from = new DateTime(y, 1, 1);
            var to = from.AddYears(1);
            return BuildReport("year", from, to, selectedYear: y);
        }

        private async Task<IActionResult> BuildReport(
            string reportType,
            DateTime from,
            DateTime to,
            int? selectedYear = null,
            int? selectedMonth = null,
            DateTime? selectedDate = null)
        {
            try
            {
                var saleServiceUrl = await _serviceResolver.GetRequiredAsync("SaleService");
                var httpClient = CreateAuthenticatedHttpClient();

                var invoices = await httpClient.GetFromJsonAsync<List<SaleInvoiceDisplayViewModel>>($"{saleServiceUrl}/api/sales") ?? new();

                var filtered = invoices
                    .Where(i => i.CreatedAt >= from && i.CreatedAt < to)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToList();

                var paid = filtered.Where(i => string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).ToList();
                var pending = filtered.Where(i => !string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).ToList();

                var topDrugs = filtered
                    .SelectMany(inv => inv.Items.Select(it => new
                    {
                        it.DrugId,
                        it.DrugName,
                        it.Quantity,
                        it.LineTotal
                    }))
                    .GroupBy(x => new { x.DrugId, x.DrugName })
                    .Select(g => new ReportTopDrugViewModel
                    {
                        DrugId = g.Key.DrugId,
                        DrugName = g.Key.DrugName,
                        Quantity = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.LineTotal)
                    })
                    .OrderByDescending(x => x.Revenue)
                    .ThenByDescending(x => x.Quantity)
                    .Take(10)
                    .ToList();

                var vm = new SalesReportViewModel
                {
                    ReportType = reportType,
                    From = from,
                    To = to,
                    InvoiceCount = filtered.Count,
                    PaidCount = paid.Count,
                    PendingCount = pending.Count,
                    TotalRevenue = filtered.Sum(x => x.TotalAmount),
                    PaidRevenue = paid.Sum(x => x.TotalAmount),
                    TopDrugs = topDrugs,
                    Invoices = filtered,
                    SelectedYear = selectedYear,
                    SelectedMonth = selectedMonth,
                    SelectedDate = selectedDate
                };

                return View(reportType switch
                {
                    "day" => "Day",
                    "month" => "Month",
                    _ => "Year"
                }, vm);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi tạo báo cáo ({reportType}): {ex.Message}");
                ViewBag.ErrorMessage = ex.Message;
                var vm = new SalesReportViewModel
                {
                    ReportType = reportType,
                    From = from,
                    To = to,
                    SelectedYear = selectedYear,
                    SelectedMonth = selectedMonth,
                    SelectedDate = selectedDate
                };
                return View(reportType switch
                {
                    "day" => "Day",
                    "month" => "Month",
                    _ => "Year"
                }, vm);
            }
        }
    }
}
