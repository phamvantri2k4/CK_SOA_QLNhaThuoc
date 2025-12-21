using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize(Policy = "OwnerOnly")]
    public class CustomersController : BaseController
    {
        private readonly IServiceResolver _resolver;

        public CustomersController(IServiceResolver resolver)
        {
            _resolver = resolver;
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> CustomerClientAsync()
        {
            var url = await _resolver.GetRequiredAsync("CustomerService");
            return (CreateAuthenticatedHttpClient(), url);
        }

        private async Task<(HttpClient client, string url)> SaleClientAsync()
        {
            var url = await _resolver.GetRequiredAsync("SaleService");
            return (CreateAuthenticatedHttpClient(), url);
        }

        /* ================= DANH SÁCH ================= */

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                var (client, url) = await CustomerClientAsync();

                var customers = await client.GetFromJsonAsync<List<CustomerViewModel>>(
                    $"{url}/api/customers") ?? new();

                return View(PagedList<CustomerViewModel>.Create(customers, page, pageSize));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải danh sách khách hàng: {ex.Message}";
                return View(PagedList<CustomerViewModel>.Create(new List<CustomerViewModel>(), page, pageSize));
            }
        }

        /* ================= CHI TIẾT ================= */

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var (client, url) = await CustomerClientAsync();
                var customer = await client.GetFromJsonAsync<CustomerViewModel>(
                    $"{url}/api/customers/{id}");

                if (customer == null) return NotFound();

                var (saleClient, saleUrl) = await SaleClientAsync();
                var sales = await saleClient.GetFromJsonAsync<List<SaleInvoiceDisplayViewModel>>(
                    $"{saleUrl}/api/sales") ?? new();

                ViewBag.PurchaseHistory = sales
                    .Where(s => s.CustomerName == customer.Name)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToList();

                return View(customer);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải thông tin khách hàng: {ex.Message}";
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /* ================= CREATE ================= */

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCustomerViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var (client, url) = await CustomerClientAsync();

            await client.PostAsJsonAsync($"{url}/api/customers", new
            {
                m.Name,
                m.Phone
            });

            TempData["SuccessMessage"] = "Tạo khách hàng thành công";
            return RedirectToAction(nameof(Index));
        }

        /* ================= EDIT ================= */

        public async Task<IActionResult> Edit(int id)
        {
            var (client, url) = await CustomerClientAsync();

            var customer = await client.GetFromJsonAsync<CustomerViewModel>(
                $"{url}/api/customers/{id}");

            if (customer == null) return NotFound();

            return View(new EditCustomerViewModel
            {
                Id = customer.Id,
                Name = customer.Name,
                Phone = customer.Phone
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditCustomerViewModel m)
        {
            if (id != m.Id || !ModelState.IsValid)
                return View(m);

            var (client, url) = await CustomerClientAsync();

            await client.PutAsJsonAsync($"{url}/api/customers/{id}", new
            {
                m.Name,
                m.Phone
            });

            TempData["SuccessMessage"] = "Cập nhật khách hàng thành công";
            return RedirectToAction(nameof(Index));
        }

        /* ================= DELETE ================= */

        public async Task<IActionResult> Delete(int id)
        {
            var (client, url) = await CustomerClientAsync();

            var customer = await client.GetFromJsonAsync<CustomerViewModel>(
                $"{url}/api/customers/{id}");

            if (customer == null) return NotFound();

            return View(customer);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var (client, url) = await CustomerClientAsync();
            await client.DeleteAsync($"{url}/api/customers/{id}");

            TempData["SuccessMessage"] = "Đã xóa khách hàng";
            return RedirectToAction(nameof(Index));
        }
    }
}
