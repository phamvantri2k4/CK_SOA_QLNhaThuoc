using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CustomerService.Data;
using CustomerService.Models;
using System.Net.Http.Json;

namespace CustomerService.Controllers
{
    /// <summary>
    /// Controller quản lý khách hàng
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomersController : ControllerBase
    {
        private readonly CustomerDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            CustomerDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<CustomersController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tất cả khách hàng
        /// GET /api/customers
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers.ToListAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách khách hàng: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy danh sách khách hàng" });
            }
        }

        /// <summary>
        /// Lấy thông tin khách hàng theo ID
        /// GET /api/customers/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(int id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);

                if (customer == null)
                {
                    _logger.LogWarning($"Không tìm thấy khách hàng với ID: {id}");
                    return NotFound(new { message = $"Không tìm thấy khách hàng với ID: {id}" });
                }

                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy thông tin khách hàng ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy thông tin khách hàng" });
            }
        }

        /// <summary>
        /// Tìm khách hàng theo số điện thoại
        /// GET /api/customers/search?phone={phone}
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<Customer>> SearchCustomerByPhone([FromQuery] string phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                {
                    return BadRequest(new { message = "Số điện thoại không được để trống" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Phone == phone.Trim());

                if (customer == null)
                {
                    return NotFound(new { message = $"Không tìm thấy khách hàng với số điện thoại: {phone}" });
                }

                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tìm khách hàng theo SĐT {phone}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi tìm khách hàng" });
            }
        }

        /// <summary>
        /// Tìm hoặc tạo khách hàng mới theo tên và số điện thoại
        /// Nếu đã có khách hàng với SĐT này thì trả về (KHÔNG tạo mới), nếu chưa thì tạo mới
        /// POST /api/customers/find-or-create
        /// </summary>
        [HttpPost("find-or-create")]
        public async Task<ActionResult<Customer>> FindOrCreateCustomer([FromBody] FindOrCreateCustomerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Tên khách hàng không được để trống" });
                }

                if (string.IsNullOrWhiteSpace(request.Phone))
                {
                    return BadRequest(new { message = "Số điện thoại không được để trống" });
                }

                var phoneTrimmed = request.Phone.Trim();
                var nameTrimmed = request.Name.Trim();

                // Tìm khách hàng theo số điện thoại (không phân biệt hoa thường, trim)
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Phone.Trim() == phoneTrimmed);

                if (existingCustomer != null)
                {
                    // Nếu đã có khách hàng với SĐT này → KHÔNG tạo mới, chỉ trả về khách hàng đã có
                    _logger.LogInformation($"Tìm thấy khách hàng đã tồn tại: {existingCustomer.Name} - {existingCustomer.Phone} (ID: {existingCustomer.Id}). Không tạo mới.");
                    
                    // Cập nhật tên nếu khác (có thể khách hàng đổi tên)
                    if (existingCustomer.Name.Trim() != nameTrimmed)
                    {
                        var oldName = existingCustomer.Name;
                        existingCustomer.Name = nameTrimmed;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Cập nhật tên khách hàng ID {existingCustomer.Id}: '{oldName}' → '{nameTrimmed}'");
                    }
                    
                    return Ok(existingCustomer);
                }

                // Nếu chưa có khách hàng với SĐT này → Tạo mới
                var newCustomer = new Customer
                {
                    Name = nameTrimmed,
                    Phone = phoneTrimmed
                };

                _context.Customers.Add(newCustomer);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Tạo mới khách hàng: {newCustomer.Name} - {newCustomer.Phone} (ID: {newCustomer.Id})");
                return CreatedAtAction(nameof(GetCustomer), new { id = newCustomer.Id }, newCustomer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tìm/tạo khách hàng: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi tìm/tạo khách hàng" });
            }
        }

        /// <summary>
        /// Tạo khách hàng mới
        /// POST /api/customers
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Customer>> CreateCustomer([FromBody] CreateCustomerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Tên khách hàng không được để trống" });
                }

                if (string.IsNullOrWhiteSpace(request.Phone))
                {
                    return BadRequest(new { message = "Số điện thoại không được để trống" });
                }

                // Kiểm tra số điện thoại đã tồn tại chưa
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Phone == request.Phone.Trim());

                if (existingCustomer != null)
                {
                    return Conflict(new { message = $"Số điện thoại {request.Phone} đã được sử dụng bởi khách hàng khác" });
                }

                var customer = new Customer
                {
                    Name = request.Name.Trim(),
                    Phone = request.Phone.Trim()
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Tạo mới khách hàng: {customer.Name} - {customer.Phone} (ID: {customer.Id})");
                return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tạo khách hàng: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi tạo khách hàng" });
            }
        }

        /// <summary>
        /// Cập nhật thông tin khách hàng
        /// PUT /api/customers/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerRequest request)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);

                if (customer == null)
                {
                    return NotFound(new { message = $"Không tìm thấy khách hàng với ID: {id}" });
                }

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Tên khách hàng không được để trống" });
                }

                if (string.IsNullOrWhiteSpace(request.Phone))
                {
                    return BadRequest(new { message = "Số điện thoại không được để trống" });
                }

                // Kiểm tra số điện thoại đã được sử dụng bởi khách hàng khác chưa
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Phone == request.Phone.Trim() && c.Id != id);

                if (existingCustomer != null)
                {
                    return Conflict(new { message = $"Số điện thoại {request.Phone} đã được sử dụng bởi khách hàng khác" });
                }

                customer.Name = request.Name.Trim();
                customer.Phone = request.Phone.Trim();

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Cập nhật khách hàng ID {id}: {customer.Name} - {customer.Phone}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi cập nhật khách hàng ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi cập nhật khách hàng" });
            }
        }

        /// <summary>
        /// Xóa khách hàng
        /// DELETE /api/customers/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);

                if (customer == null)
                {
                    return NotFound(new { message = $"Không tìm thấy khách hàng với ID: {id}" });
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Xóa khách hàng ID {id}: {customer.Name} - {customer.Phone}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xóa khách hàng ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi xóa khách hàng" });
            }
        }

        /// <summary>
        /// Lấy lịch sử mua hàng của khách hàng
        /// GET /api/customers/{id}/purchase-history
        /// </summary>
        [HttpGet("{id}/purchase-history")]
        public async Task<ActionResult<IEnumerable<PurchaseHistoryItem>>> GetPurchaseHistory(int id)
        {
            try
            {
                // Kiểm tra khách hàng có tồn tại không
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new { message = $"Không tìm thấy khách hàng với ID: {id}" });
                }

                // Gọi SaleService để lấy danh sách hóa đơn
                var saleServiceUrl = _configuration["SaleService:BaseUrl"] ?? "http://localhost:5002";
                var authServiceUrl = _configuration["AuthService:BaseUrl"] ?? "http://localhost:5004";
                var httpClient = _httpClientFactory.CreateClient();

                // Lấy service token để gọi SaleService
                var serviceToken = await GetServiceTokenAsync(httpClient, authServiceUrl);
                if (!string.IsNullOrEmpty(serviceToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
                }

                _logger.LogInformation($"Gọi SaleService để lấy lịch sử mua hàng của khách hàng ID {id} tại {saleServiceUrl}");

                var response = await httpClient.GetAsync($"{saleServiceUrl}/api/sales");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Không thể lấy danh sách hóa đơn từ SaleService. Status: {response.StatusCode}");
                    return StatusCode(500, new { message = "Không thể lấy lịch sử mua hàng từ SaleService" });
                }

                var allSales = await response.Content.ReadFromJsonAsync<List<SaleInvoiceDto>>();
                
                if (allSales == null)
                {
                    return Ok(new List<PurchaseHistoryItem>());
                }

                // Lọc hóa đơn theo CustomerId
                var customerSales = allSales
                    .Where(s => s.CustomerId == id)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new PurchaseHistoryItem
                    {
                        InvoiceId = s.Id,
                        CreatedAt = s.CreatedAt,
                        TotalAmount = s.TotalAmount,
                        ItemCount = s.Items?.Count ?? 0,
                        Items = s.Items?.Select(i => new PurchaseHistoryItemDetail
                        {
                            DrugId = i.DrugId,
                            DrugName = i.DrugName,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            LineTotal = i.LineTotal
                        }).ToList() ?? new List<PurchaseHistoryItemDetail>()
                    })
                    .ToList();

                _logger.LogInformation($"Tìm thấy {customerSales.Count} hóa đơn của khách hàng ID {id}");
                return Ok(customerSales);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy lịch sử mua hàng của khách hàng ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy lịch sử mua hàng" });
            }
        }

        /// <summary>
        /// Helper method để lấy service token từ AuthService
        /// </summary>
        private static string? _cachedServiceToken;
        private static DateTime _serviceTokenExpiry = DateTime.MinValue;
        private const string ServiceKey = "ServiceKey123!";

        private async Task<string?> GetServiceTokenAsync(HttpClient httpClient, string authServiceUrl)
        {
            // Nếu token còn hiệu lực, dùng lại
            if (!string.IsNullOrEmpty(_cachedServiceToken) && DateTime.UtcNow < _serviceTokenExpiry)
            {
                return _cachedServiceToken;
            }

            try
            {
                var request = new { ServiceKey = ServiceKey };
                var response = await httpClient.PostAsJsonAsync($"{authServiceUrl}/api/auth/service-token", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    _cachedServiceToken = result.GetProperty("token").GetString();
                    _serviceTokenExpiry = DateTime.UtcNow.AddHours(23); // Token hết hạn sau 23 giờ
                    return _cachedServiceToken;
                }
            }
            catch
            {
                // Log error nếu cần
            }

            return null;
        }
    }

    // DTOs
    public class CreateCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class UpdateCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class FindOrCreateCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    // DTOs cho lịch sử mua hàng
    public class SaleInvoiceDto
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CustomerId { get; set; }
        public int StaffId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<SaleItemDto>? Items { get; set; }
    }

    public class SaleItemDto
    {
        public int DrugId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class PurchaseHistoryItem
    {
        public int InvoiceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
        public List<PurchaseHistoryItemDetail> Items { get; set; } = new();
    }

    public class PurchaseHistoryItemDetail
    {
        public int DrugId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}

