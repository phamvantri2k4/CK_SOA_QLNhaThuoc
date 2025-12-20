using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// Controller quản lý khách hàng (dành cho Admin)
    /// </summary>
    [Authorize(Policy = "OwnerOnly")]
    public class CustomersController : BaseController
    {
        private readonly ILogger<CustomersController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public CustomersController(ILogger<CustomersController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        private Task<string> CustomerServiceUrlAsync => _serviceResolver.GetRequiredAsync("CustomerService");
        private Task<string> SaleServiceUrlAsync => _serviceResolver.GetRequiredAsync("SaleService");

        /// <summary>
        /// Danh sách khách hàng (có phân trang)
        /// GET /Customers?page=1pageSize=10
        /// </summary>
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                var customerServiceUrl = await CustomerServiceUrlAsync;

                var response = await httpClient.GetAsync($"{customerServiceUrl}/api/customers");

                if (response.IsSuccessStatusCode)
                {
                    var customers = await response.Content.ReadFromJsonAsync<List<CustomerViewModel>>();
                    var allCustomers = customers ?? new List<CustomerViewModel>();
                    
                    // Phân trang
                    var pagedList = PagedList<CustomerViewModel>.Create(allCustomers, page, pageSize);
                    return View(pagedList);
                }
                else
                {
                    _logger.LogError($"Lỗi khi lấy danh sách khách hàng. Status: {response.StatusCode}");
                    ViewBag.ErrorMessage = "Không thể lấy danh sách khách hàng";
                    var emptyList = new List<CustomerViewModel>();
                    var pagedList = PagedList<CustomerViewModel>.Create(emptyList, 1, pageSize);
                    return View(pagedList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách khách hàng: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                var emptyList = new List<CustomerViewModel>();
                var pagedList = PagedList<CustomerViewModel>.Create(emptyList, 1, pageSize);
                return View(pagedList);
            }
        }

        /// <summary>
        /// Chi tiết khách hàng và lịch sử mua hàng
        /// GET /Customers/Details/{id}
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                var customerServiceUrl = await CustomerServiceUrlAsync;

                var customerResponse = await httpClient.GetAsync($"{customerServiceUrl}/api/customers/{id}");
                if (!customerResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Không tìm thấy khách hàng với ID: {id}");
                    return NotFound();
                }

                var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerViewModel>();
                if (customer == null)
                {
                    return NotFound();
                }

                var saleServiceUrl = await SaleServiceUrlAsync;
                var salesResponse = await httpClient.GetAsync($"{saleServiceUrl}/api/sales");
                var purchaseHistory = new List<SaleInvoiceDisplayViewModel>();

                if (salesResponse.IsSuccessStatusCode)
                {
                    var allSales = await salesResponse.Content.ReadFromJsonAsync<List<SaleInvoiceDisplayViewModel>>();
                    purchaseHistory = allSales?
                        .Where(s => s.CustomerName?.Contains(customer.Name, StringComparison.OrdinalIgnoreCase) == true)
                        .OrderByDescending(s => s.CreatedAt)
                        .ToList() ?? new List<SaleInvoiceDisplayViewModel>();
                }

                ViewBag.PurchaseHistory = purchaseHistory;
                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết khách hàng ID {id}: {ex.Message}");
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                return View();
            }
        }

        /// <summary>
        /// Form tạo khách hàng mới
        /// GET /Customers/Create
        /// </summary>
        public IActionResult Create()
        {
            return View();
        }

        /// <summary>
        /// Xử lý tạo khách hàng mới
        /// POST /Customers/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCustomerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                var customerServiceUrl = await CustomerServiceUrlAsync;

                var request = new
                {
                    name = model.Name,
                    phone = model.Phone
                };

                var response = await httpClient.PostAsJsonAsync($"{customerServiceUrl}/api/customers", request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Tạo khách hàng thành công: {model.Name}");
                    TempData["SuccessMessage"] = "Tạo khách hàng thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Lỗi khi tạo khách hàng: {error}");
                    ModelState.AddModelError("", $"Lỗi: {error}");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tạo khách hàng: {ex.Message}");
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                return View(model);
            }
        }

        /// <summary>
        /// Form sửa khách hàng
        /// GET /Customers/Edit/{id}
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                var customerServiceUrl = await CustomerServiceUrlAsync;

                var response = await httpClient.GetAsync($"{customerServiceUrl}/api/customers/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    return NotFound();
                }

                var customer = await response.Content.ReadFromJsonAsync<CustomerViewModel>();

                if (customer == null)
                {
                    return NotFound();
                }

                var model = new EditCustomerViewModel
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    Phone = customer.Phone
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy thông tin khách hàng ID {id}: {ex.Message}");
                return NotFound();
            }
        }

        /// <summary>
        /// Xử lý cập nhật khách hàng
        /// POST /Customers/Edit/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditCustomerViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                var customerServiceUrl = await CustomerServiceUrlAsync;

                var request = new
                {
                    name = model.Name,
                    phone = model.Phone
                };

                var response = await httpClient.PutAsJsonAsync($"{customerServiceUrl}/api/customers/{id}", request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Cập nhật khách hàng thành công: ID {id}");
                    TempData["SuccessMessage"] = "Cập nhật khách hàng thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Lỗi khi cập nhật khách hàng: {error}");
                    ModelState.AddModelError("", $"Lỗi: {error}");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi cập nhật khách hàng: {ex.Message}");
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                return View(model);
            }
        }

        /// <summary>
        /// Xác nhận xóa khách hàng
        /// GET /Customers/Delete/{id}
        /// </summary>
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                var customerServiceUrl = await CustomerServiceUrlAsync;

                var response = await httpClient.GetAsync($"{customerServiceUrl}/api/customers/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    return NotFound();
                }

                var customer = await response.Content.ReadFromJsonAsync<CustomerViewModel>();
                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy thông tin khách hàng ID {id}: {ex.Message}");
                return NotFound();
            }
        }

        /// <summary>
        /// Xử lý xóa khách hàng
        /// POST /Customers/Delete/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                var customerServiceUrl = await CustomerServiceUrlAsync;

                var response = await httpClient.DeleteAsync($"{customerServiceUrl}/api/customers/{id}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Xóa khách hàng thành công: ID {id}");
                    TempData["SuccessMessage"] = "Xóa khách hàng thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    _logger.LogWarning($"Lỗi khi xóa khách hàng. Status: {response.StatusCode}");
                    TempData["ErrorMessage"] = "Không thể xóa khách hàng";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xóa khách hàng: {ex.Message}");
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}

