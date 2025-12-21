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
        private readonly IServiceResolver _resolver;

        public ReportsController(IServiceResolver resolver)
        {
            _resolver = resolver;
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> GetClientAsync()
        {
            var url = await _resolver.GetRequiredAsync("ReportingService");
            return (CreateAuthenticatedHttpClient(), url);
        }

        private async Task<IActionResult> LoadReportAsync(
            string apiUrl,
            string viewName,
            string reportType)
        {
            try
            {
                var (client, url) = await GetClientAsync();

                var report = await client.GetFromJsonAsync<SalesReportViewModel>(
                    $"{url}{apiUrl}");

                return View(viewName, report ?? new SalesReportViewModel
                {
                    ReportType = reportType
                });
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải báo cáo: {ex.Message}";
                return View(viewName, new SalesReportViewModel
                {
                    ReportType = reportType
                });
            }
        }

        /* ================= BÁO CÁO NGÀY ================= */

        [HttpGet]
        public async Task<IActionResult> Day(DateTime? date)
        {
            var d = date ?? DateTime.Today;
            return await LoadReportAsync(
                $"/api/reports/day?date={d:yyyy-MM-dd}",
                "Day_New",
                "day");
        }

        /* ================= BÁO CÁO THÁNG ================= */

        [HttpGet]
        public async Task<IActionResult> Month(int? year, int? month)
        {
            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;

            return await LoadReportAsync(
                $"/api/reports/month?year={y}&month={m}",
                "Month_New",
                "month");
        }

        /* ================= BÁO CÁO NĂM ================= */

        [HttpGet]
        public async Task<IActionResult> Year(int? year)
        {
            var y = year ?? DateTime.Today.Year;

            return await LoadReportAsync(
                $"/api/reports/year?year={y}",
                "Year_New",
                "year");
        }
    }
}
