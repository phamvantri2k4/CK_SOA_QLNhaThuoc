using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaleService.Data;
using SaleService.Helpers;
using SaleService.Model;
using SaleService.Models;
using Shared;
using System.Net.Http.Json;

namespace SaleService.Controllers
{
    /// <summary>
    /// Controller quản lý hóa đơn bán hàng (Sale Invoice)
    /// Tích hợp với DrugService để lấy thông tin thuốc
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly SaleDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            SaleDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<SalesController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tất cả hóa đơn
        /// GET /api/sales?staffId=1
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SaleInvoiceResponse>>> GetSales([FromQuery] int? staffId = null)
        {
            try
            {
                var query = _context.SaleInvoices
                    .Include(s => s.Details)
                    .AsQueryable();

                // Filter theo nhân viên nếu có
                if (staffId.HasValue && staffId.Value > 0)
                {
                    query = query.Where(s => s.StaffId == staffId.Value);
                    _logger.LogInformation($"Lọc hóa đơn theo nhân viên ID: {staffId.Value}");
                }

                var invoices = await query.ToListAsync();

                var response = new List<SaleInvoiceResponse>();

                foreach (var invoice in invoices)
                {
                    var items = new List<SaleItemResponse>();
                    
                    foreach (var detail in invoice.Details)
                    {
                        // Lấy thông tin thuốc từ DrugService
                        var drug = await GetDrugFromServiceAsync(detail.DrugId);
                        
                        items.Add(new SaleItemResponse
                        {
                            DrugId = detail.DrugId,
                            DrugName = drug?.Name ?? "Unknown",
                            UnitType = string.IsNullOrWhiteSpace(detail.UnitType) ? "pill" : detail.UnitType.Trim().ToLowerInvariant(),
                            Quantity = detail.Quantity,
                            UnitPrice = detail.UnitPrice,
                            LineTotal = detail.Quantity * detail.UnitPrice
                        });
                    }

                    // Lấy tên khách hàng từ CustomerService
                    string customerName = "Khách vãng lai";
                    if (invoice.CustomerId.HasValue)
                    {
                        var customer = await GetCustomerFromServiceAsync(invoice.CustomerId.Value);
                        if (customer != null)
                        {
                            customerName = customer.Name;
                        }
                    }

                    // Lấy tên nhân viên từ AuthService
                    string staffName = $"NV-{invoice.StaffId}";
                    var staff = await GetStaffFromServiceAsync(invoice.StaffId);
                    if (staff != null && !string.IsNullOrWhiteSpace(staff.FullName))
                    {
                        staffName = staff.FullName;
                        _logger.LogInformation($"Đã lấy FullName cho nhân viên ID {invoice.StaffId}: {staffName}");
                    }
                    else
                    {
                        _logger.LogWarning($"Không thể lấy FullName cho nhân viên ID {invoice.StaffId}, sử dụng mặc định: {staffName}");
                    }

                    response.Add(new SaleInvoiceResponse
                    {
                        Id = invoice.Id,
                        CreatedAt = invoice.CreatedAt,
                        CustomerId = invoice.CustomerId,
                        CustomerName = customerName,
                        StaffId = invoice.StaffId,
                        StaffName = staffName,
                        TotalAmount = invoice.TotalAmount,
                        PaymentStatus = invoice.PaymentStatus,
                        PaidAt = invoice.PaidAt,
                        Items = items
                    });
                }

                _logger.LogInformation($"Trả về danh sách {response.Count} hóa đơn");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách hóa đơn: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy danh sách hóa đơn" });
            }
        }

        /// <summary>
        /// Lấy chi tiết một hóa đơn theo ID
        /// GET /api/sales/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SaleInvoiceResponse>> GetSale(int id)
        {
            try
            {
                var invoice = await _context.SaleInvoices
                    .Include(s => s.Details)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (invoice == null)
                {
                    _logger.LogWarning($"Không tìm thấy hóa đơn với ID: {id}");
                    return NotFound(new { message = $"Không tìm thấy hóa đơn với ID: {id}" });
                }

                var items = new List<SaleItemResponse>();
                
                foreach (var detail in invoice.Details)
                {
                    // Lấy thông tin thuốc từ DrugService
                    var drug = await GetDrugFromServiceAsync(detail.DrugId);
                    
                    items.Add(new SaleItemResponse
                    {
                        DrugId = detail.DrugId,
                        DrugName = drug?.Name ?? "Unknown",
                        UnitType = string.IsNullOrWhiteSpace(detail.UnitType) ? "pill" : detail.UnitType.Trim().ToLowerInvariant(),
                        Quantity = detail.Quantity,
                        UnitPrice = detail.UnitPrice,
                        LineTotal = detail.Quantity * detail.UnitPrice
                    });
                }

                // Lấy tên khách hàng từ CustomerService
                string customerName = "Khách vãng lai";
                if (invoice.CustomerId.HasValue)
                {
                    var customer = await GetCustomerFromServiceAsync(invoice.CustomerId.Value);
                    if (customer != null)
                    {
                        customerName = customer.Name;
                    }
                }

                // Lấy tên nhân viên từ AuthService
                string staffName = $"NV-{invoice.StaffId}";
                var staff = await GetStaffFromServiceAsync(invoice.StaffId);
                if (staff != null)
                {
                    staffName = staff.FullName;
                }

                var response = new SaleInvoiceResponse
                {
                    Id = invoice.Id,
                    CreatedAt = invoice.CreatedAt,
                    CustomerId = invoice.CustomerId,
                    CustomerName = customerName,
                    StaffId = invoice.StaffId,
                    StaffName = staffName,
                    TotalAmount = invoice.TotalAmount,
                    PaymentStatus = invoice.PaymentStatus,
                    PaidAt = invoice.PaidAt,
                    Items = items
                };

                _logger.LogInformation($"Trả về chi tiết hóa đơn ID: {id}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết hóa đơn ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy chi tiết hóa đơn" });
            }
        }

        /// <summary>
        /// Đánh dấu hóa đơn đã thanh toán
        /// PUT /api/sales/{id}/pay
        /// </summary>
        [HttpPut("{id}/pay")]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            try
            {
                var invoice = await _context.SaleInvoices
                    .Include(s => s.Details)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (invoice == null)
                {
                    _logger.LogWarning($"Không tìm thấy hóa đơn với ID: {id}");
                    return NotFound(new { message = $"Không tìm thấy hóa đơn với ID: {id}" });
                }

                if (invoice.PaymentStatus == "Paid")
                {
                    return BadRequest(new { message = "Hóa đơn này đã được thanh toán rồi" });
                }

                var inventoryBaseUrl = await ResolveInventoryBaseUrlAsync();
                var httpClient = _httpClientFactory.CreateClient();

                List<InventoryItemDto> inventoryItems;
                try
                {
                    inventoryItems = await httpClient.GetFromJsonAsync<List<InventoryItemDto>>($"{inventoryBaseUrl}/api/inventory/status")
                        ?? new List<InventoryItemDto>();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Không thể lấy tồn kho từ InventoryService: {ex.Message}");
                    return StatusCode(502, new { message = "Không thể kết nối InventoryService để kiểm tra tồn kho" });
                }

                var availableByDrug = inventoryItems
                    .GroupBy(x => x.DrugId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                var requiredByDrug = new Dictionary<int, int>();
                foreach (var detail in invoice.Details)
                {
                    var unitType = string.IsNullOrWhiteSpace(detail.UnitType) ? "pill" : detail.UnitType.Trim().ToLowerInvariant();
                    if (unitType != "pill" && unitType != "box")
                    {
                        return BadRequest(new { message = $"UnitType không hợp lệ trong hóa đơn: {detail.UnitType}. Chỉ hỗ trợ 'pill' hoặc 'box'" });
                    }

                    var qty = detail.Quantity;
                    if (qty <= 0)
                    {
                        return BadRequest(new { message = $"Số lượng không hợp lệ cho DrugId={detail.DrugId}" });
                    }

                    var exportQty = qty;
                    if (unitType == "box")
                    {
                        var drug = await GetDrugFromServiceAsync(detail.DrugId);
                        var packSize = drug?.PackSize > 0 ? drug.PackSize : 1;
                        exportQty = checked(qty * packSize);
                    }

                    if (requiredByDrug.ContainsKey(detail.DrugId))
                    {
                        requiredByDrug[detail.DrugId] += exportQty;
                    }
                    else
                    {
                        requiredByDrug[detail.DrugId] = exportQty;
                    }
                }

                foreach (var kv in requiredByDrug)
                {
                    availableByDrug.TryGetValue(kv.Key, out var available);
                    if (available < kv.Value)
                    {
                        var drug = await GetDrugFromServiceAsync(kv.Key);
                        var name = drug?.Name ?? $"#{kv.Key}";
                        return BadRequest(new
                        {
                            message = $"Không đủ tồn kho cho thuốc '{name}'. Cần {kv.Value} viên nhưng chỉ còn {available} viên"
                        });
                    }
                }

                foreach (var kv in requiredByDrug)
                {
                    var payload = new { drugId = kv.Key, quantity = kv.Value };
                    var exportResponse = await httpClient.PostAsJsonAsync($"{inventoryBaseUrl}/api/inventory/export", payload);
                    if (!exportResponse.IsSuccessStatusCode)
                    {
                        var err = await exportResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning($"Xuất kho thất bại cho DrugId={kv.Key}, Quantity={kv.Value}: {err}");
                        return BadRequest(new { message = $"Xuất kho thất bại cho DrugId={kv.Key}: {err}" });
                    }
                }

                invoice.PaymentStatus = "Paid";
                invoice.PaidAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Đã đánh dấu hóa đơn {id} là đã thanh toán");

                return Ok(new { message = "Thanh toán thành công", invoiceId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi thanh toán hóa đơn {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi thanh toán hóa đơn" });
            }
        }

        private async Task<string> ResolveInventoryBaseUrlAsync()
        {
            var registryUrl = _configuration["ServiceRegistry:BaseUrl"] ?? "http://localhost:6000";
            var fallback = _configuration["InventoryService:BaseUrl"] ?? "http://localhost:5006";

            try
            {
                var discoveryClient = new ServiceDiscoveryClient(registryUrl);
                var service = await discoveryClient.FindServiceAsync("InventoryService");
                if (service != null && !string.IsNullOrWhiteSpace(service.Url))
                {
                    return service.Url.TrimEnd('/');
                }
            }
            catch
            {
            }

            return fallback.TrimEnd('/');
        }

        private class InventoryItemDto
        {
            public int Id { get; set; }
            public int DrugId { get; set; }
            public int Quantity { get; set; }
            public DateTime? ExpiryDate { get; set; }
        }

        /// <summary>
        /// Xóa hóa đơn
        /// DELETE /api/sales/{id}
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteSale(int id)
        {
            try
            {
                var invoice = await _context.SaleInvoices
                    .Include(s => s.Details)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (invoice == null)
                {
                    _logger.LogWarning($"Không tìm thấy hóa đơn với ID: {id}");
                    return NotFound(new { message = $"Không tìm thấy hóa đơn với ID: {id}" });
                }

                // Xóa chi tiết hóa đơn
                _context.SaleInvoiceDetails.RemoveRange(invoice.Details);
                
                // Xóa hóa đơn
                _context.SaleInvoices.Remove(invoice);
                
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Đã xóa hóa đơn {id}");

                return Ok(new { message = "Đã xóa hóa đơn thành công", invoiceId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xóa hóa đơn {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi xóa hóa đơn" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult<SaleInvoiceResponse>> UpdateSale(int id, [FromBody] UpdateSaleRequest request)
        {
            try
            {
                var invoice = await _context.SaleInvoices
                    .Include(s => s.Details)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (invoice == null)
                {
                    return NotFound(new { message = $"Không tìm thấy hóa đơn với ID: {id}" });
                }

                if (invoice.PaymentStatus == "Paid")
                {
                    return BadRequest(new { message = "Không thể sửa hóa đơn đã thanh toán" });
                }

                if (request.Items == null || request.Items.Count == 0)
                {
                    return BadRequest(new { message = "Hóa đơn phải có ít nhất một sản phẩm" });
                }

                int? customerId = null;
                var customerName = "Khách vãng lai";

                if (!string.IsNullOrWhiteSpace(request.CustomerName) && !string.IsNullOrWhiteSpace(request.CustomerPhone))
                {
                    var customer = await GetOrCreateCustomerFromServiceAsync(
                        request.CustomerName.Trim(),
                        request.CustomerPhone.Trim());

                    if (customer != null)
                    {
                        customerId = customer.Id;
                        customerName = customer.Name;
                    }
                }

                invoice.CustomerId = customerId;

                _context.SaleInvoiceDetails.RemoveRange(invoice.Details);
                invoice.Details.Clear();

                var items = new List<SaleItemResponse>();
                decimal totalAmount = 0;

                foreach (var item in request.Items)
                {
                    if (item.DrugId <= 0)
                    {
                        return BadRequest(new { message = $"DrugId không hợp lệ: {item.DrugId}" });
                    }

                    if (item.Quantity <= 0)
                    {
                        return BadRequest(new { message = $"Số lượng không hợp lệ cho DrugId={item.DrugId}" });
                    }

                    var drug = await GetDrugFromServiceAsync(item.DrugId);
                    if (drug == null)
                    {
                        return BadRequest(new { message = $"Không tìm thấy thuốc với ID: {item.DrugId}. Vui lòng kiểm tra DrugService." });
                    }

                    var unitType = string.IsNullOrWhiteSpace(item.UnitType) ? "pill" : item.UnitType.Trim().ToLowerInvariant();
                    if (unitType != "pill" && unitType != "box")
                    {
                        return BadRequest(new { message = $"UnitType không hợp lệ: {item.UnitType}. Chỉ hỗ trợ 'pill' hoặc 'box'" });
                    }

                    var unitPrice = unitType == "box" ? drug.BoxPrice : drug.SellPrice;
                    if (unitPrice <= 0)
                    {
                        return BadRequest(new { message = $"Thuốc '{drug.Name}' chưa có giá cho dạng '{unitType}'" });
                    }

                    var detail = new SaleInvoiceDetail
                    {
                        DrugId = item.DrugId,
                        UnitType = unitType,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice
                    };

                    invoice.Details.Add(detail);

                    var lineTotal = item.Quantity * unitPrice;
                    totalAmount += lineTotal;

                    items.Add(new SaleItemResponse
                    {
                        DrugId = drug.Id,
                        DrugName = drug.Name,
                        UnitType = unitType,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        LineTotal = lineTotal
                    });
                }

                invoice.TotalAmount = totalAmount;
                await _context.SaveChangesAsync();

                string staffName = $"NV-{invoice.StaffId}";
                var staff = await GetStaffFromServiceAsync(invoice.StaffId);
                if (staff != null && !string.IsNullOrWhiteSpace(staff.FullName))
                {
                    staffName = staff.FullName;
                }

                return Ok(new SaleInvoiceResponse
                {
                    Id = invoice.Id,
                    CreatedAt = invoice.CreatedAt,
                    CustomerId = invoice.CustomerId,
                    CustomerName = customerName,
                    StaffId = invoice.StaffId,
                    StaffName = staffName,
                    TotalAmount = invoice.TotalAmount,
                    PaymentStatus = invoice.PaymentStatus,
                    PaidAt = invoice.PaidAt,
                    Items = items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi sửa hóa đơn {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi sửa hóa đơn" });
            }
        }

        /// <summary>
        /// Tạo hóa đơn mới
        /// POST /api/sales/create
        /// Gọi sang DrugService để lấy giá thuốc
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<SaleInvoiceResponse>> CreateSale([FromBody] CreateSaleRequest request)
        {
            try
            {
                // Validate dữ liệu
                if (request.Items == null || !request.Items.Any())
                {
                    return BadRequest(new { message = "Hóa đơn phải có ít nhất một sản phẩm" });
                }

                // Tìm hoặc tạo khách hàng từ CustomerService
                // Nếu SĐT đã tồn tại → sử dụng khách hàng đã có (KHÔNG tạo mới)
                // Nếu SĐT chưa có → tạo khách hàng mới
                int? customerId = null;
                string customerName = "Khách vãng lai";
                
                if (!string.IsNullOrWhiteSpace(request.CustomerName) && !string.IsNullOrWhiteSpace(request.CustomerPhone))
                {
                    var customer = await GetOrCreateCustomerFromServiceAsync(
                        request.CustomerName.Trim(), 
                        request.CustomerPhone.Trim());
                    
                    if (customer != null)
                    {
                        customerId = customer.Id;
                        customerName = customer.Name;
                        _logger.LogInformation($"Đã xử lý khách hàng: {customer.Name} - {request.CustomerPhone.Trim()} (ID: {customer.Id}). Nếu SĐT đã tồn tại thì dùng lại, nếu chưa thì đã tạo mới.");
                    }
                    else
                    {
                        _logger.LogWarning($"Không thể tìm/tạo khách hàng: {request.CustomerName} - {request.CustomerPhone}");
                    }
                }

                // Tạo hóa đơn mới
                var invoice = new SaleInvoice
                {
                    CreatedAt = DateTime.Now,
                    CustomerId = customerId,
                    StaffId = request.StaffId > 0 ? request.StaffId : 1,
                    TotalAmount = 0
                };

                var items = new List<SaleItemResponse>();
                decimal totalAmount = 0;

                // Xử lý từng item trong hóa đơn
                foreach (var item in request.Items)
                {
                    // Validate drugId
                    if (item.DrugId <= 0)
                    {
                        _logger.LogWarning($"DrugId không hợp lệ: {item.DrugId}");
                        return BadRequest(new { message = $"DrugId không hợp lệ: {item.DrugId}" });
                    }

                    // Gọi DrugService để lấy thông tin thuốc
                    _logger.LogInformation($"Đang tìm thuốc với ID: {item.DrugId}");
                    var drug = await GetDrugFromServiceAsync(item.DrugId);

                    if (drug == null)
                    {
                        _logger.LogWarning($"Không tìm thấy thuốc với ID: {item.DrugId}. Có thể DrugService không chạy hoặc thuốc không tồn tại.");
                        return BadRequest(new { message = $"Không tìm thấy thuốc với ID: {item.DrugId}. Vui lòng kiểm tra DrugService đã chạy chưa và thuốc có tồn tại không." });
                    }

                    if (item.Quantity <= 0)
                    {
                        return BadRequest(new { message = $"Số lượng thuốc '{drug.Name}' phải lớn hơn 0" });
                    }

                    var unitType = string.IsNullOrWhiteSpace(item.UnitType) ? "pill" : item.UnitType.Trim().ToLowerInvariant();
                    if (unitType != "pill" && unitType != "box")
                    {
                        return BadRequest(new { message = $"UnitType không hợp lệ: {item.UnitType}. Chỉ hỗ trợ 'pill' hoặc 'box'" });
                    }

                    var unitPrice = unitType == "box" ? drug.BoxPrice : drug.SellPrice;

                    // Tạo detail cho hóa đơn
                    var detail = new SaleInvoiceDetail
                    {
                        DrugId = item.DrugId,
                        UnitType = unitType,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice
                    };

                    invoice.Details.Add(detail);

                    // Tính tổng tiền
                    var lineTotal = item.Quantity * unitPrice;
                    totalAmount += lineTotal;

                    items.Add(new SaleItemResponse
                    {
                        DrugId = drug.Id,
                        DrugName = drug.Name,
                        UnitType = unitType,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        LineTotal = lineTotal
                    });
                }

                // Cập nhật tổng tiền
                invoice.TotalAmount = totalAmount;

                // Lưu vào database
                _context.SaleInvoices.Add(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Đã tạo hóa đơn mới: ID={invoice.Id}, TotalAmount={invoice.TotalAmount}");

                // Lấy tên nhân viên từ AuthService
                string staffName = $"NV-{invoice.StaffId}";
                var staff = await GetStaffFromServiceAsync(invoice.StaffId);
                if (staff != null)
                {
                    staffName = staff.FullName;
                }

                // Trả về response
                var response = new SaleInvoiceResponse
                {
                    Id = invoice.Id,
                    CreatedAt = invoice.CreatedAt,
                    CustomerId = invoice.CustomerId,
                    CustomerName = customerName,
                    StaffId = invoice.StaffId,
                    StaffName = staffName,
                    TotalAmount = invoice.TotalAmount,
                    PaymentStatus = invoice.PaymentStatus,
                    PaidAt = invoice.PaidAt,
                    Items = items
                };

                return CreatedAtAction(nameof(GetSale), new { id = invoice.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tạo hóa đơn: {ex.Message}");
                return StatusCode(500, new { message = $"Lỗi server khi tạo hóa đơn: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gọi CustomerService để lấy thông tin khách hàng theo ID
        /// </summary>
        private async Task<CustomerDto?> GetCustomerFromServiceAsync(int customerId)
        {
            try
            {
                var customerServiceUrl = _configuration["CustomerService:BaseUrl"] ?? "http://localhost:5003";
                var authServiceUrl = _configuration["AuthService:BaseUrl"] ?? "http://localhost:5004";
                var httpClient = _httpClientFactory.CreateClient();
                
                // Lấy service token để gọi CustomerService
                var serviceToken = await ServiceTokenHelper.GetServiceTokenAsync(httpClient, authServiceUrl);
                if (!string.IsNullOrEmpty(serviceToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
                }
                
                _logger.LogInformation($"Gọi CustomerService để lấy thông tin khách hàng ID: {customerId} tại {customerServiceUrl}");

                var response = await httpClient.GetAsync($"{customerServiceUrl}/api/customers/{customerId}");

                if (response.IsSuccessStatusCode)
                {
                    var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
                    return customer;
                }
                else
                {
                    _logger.LogWarning($"Không tìm thấy khách hàng ID {customerId}. Status: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi CustomerService: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gọi AuthService để lấy thông tin nhân viên theo ID
        /// </summary>
        private async Task<UserDto?> GetStaffFromServiceAsync(int staffId)
        {
            try
            {
                var authServiceUrl = _configuration["AuthService:BaseUrl"] ?? "http://localhost:5004";
                var httpClient = _httpClientFactory.CreateClient();
                
                // Lấy service token để gọi AuthService
                var serviceToken = await ServiceTokenHelper.GetServiceTokenAsync(httpClient, authServiceUrl);
                if (!string.IsNullOrEmpty(serviceToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
                }
                
                _logger.LogInformation($"Gọi AuthService để lấy thông tin nhân viên ID: {staffId} tại {authServiceUrl}/api/users/{staffId}");

                var response = await httpClient.GetAsync($"{authServiceUrl}/api/users/{staffId}");

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<UserDto>();
                    if (user != null)
                    {
                        _logger.LogInformation($"Lấy được thông tin nhân viên: ID={user.Id}, FullName={user.FullName}, Username={user.Username}");
                    }
                    else
                    {
                        _logger.LogWarning($"AuthService trả về null cho nhân viên ID {staffId}");
                    }
                    return user;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Không tìm thấy nhân viên ID {staffId} trong AuthService. Status: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Error content: {errorContent}");
                    return null;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning($"Không có quyền truy cập thông tin nhân viên ID {staffId}. Status: {response.StatusCode}");
                    return null;
                }
                else
                {
                    _logger.LogWarning($"Lỗi khi gọi AuthService cho nhân viên ID {staffId}. Status: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Error content: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi AuthService: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gọi CustomerService để tìm hoặc tạo khách hàng
        /// Nếu đã có khách hàng với SĐT thì trả về, nếu chưa thì tạo mới
        /// </summary>
        private async Task<CustomerDto?> GetOrCreateCustomerFromServiceAsync(string name, string phone)
        {
            try
            {
                var customerServiceUrl = _configuration["CustomerService:BaseUrl"] ?? "http://localhost:5003";
                var authServiceUrl = _configuration["AuthService:BaseUrl"] ?? "http://localhost:5004";
                var httpClient = _httpClientFactory.CreateClient();
                
                // Lấy service token để gọi CustomerService
                var serviceToken = await ServiceTokenHelper.GetServiceTokenAsync(httpClient, authServiceUrl);
                if (!string.IsNullOrEmpty(serviceToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
                }
                
                _logger.LogInformation($"Gọi CustomerService để tìm/tạo khách hàng: {name} - {phone} tại {customerServiceUrl}");

                var requestBody = new
                {
                    Name = name,
                    Phone = phone
                };

                var response = await httpClient.PostAsJsonAsync(
                    $"{customerServiceUrl}/api/customers/find-or-create", 
                    requestBody);

                if (response.IsSuccessStatusCode)
                {
                    var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
                    return customer;
                }
                else
                {
                    _logger.LogWarning($"Không thể tìm/tạo khách hàng. Status: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi CustomerService: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gọi DrugService để lấy thông tin thuốc
        /// Đây là ví dụ về Service-to-Service Communication trong SOA
        /// </summary>
        private async Task<DrugDto?> GetDrugFromServiceAsync(int drugId)
        {
            try
            {
                // Lấy URL của DrugService từ configuration
                // Trong thực tế, có thể gọi ServiceRegistry để Find service
                var drugServiceUrl = _configuration["DrugService:BaseUrl"] ?? "http://localhost:5001";
                var authServiceUrl = _configuration["AuthService:BaseUrl"] ?? "http://localhost:5004";

                var httpClient = _httpClientFactory.CreateClient();
                
                // Lấy service token để gọi DrugService
                var serviceToken = await ServiceTokenHelper.GetServiceTokenAsync(httpClient, authServiceUrl);
                if (!string.IsNullOrEmpty(serviceToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
                }
                
                _logger.LogInformation($"Gọi DrugService để lấy thông tin thuốc ID: {drugId} tại {drugServiceUrl}/api/drugs/{drugId}");

                var response = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs/{drugId}");

                if (response.IsSuccessStatusCode)
                {
                    var drug = await response.Content.ReadFromJsonAsync<DrugDto>();
                    _logger.LogInformation($"Lấy thông tin thuốc thành công: {drug?.Name} (ID: {drug?.Id})");
                    return drug;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Không tìm thấy thuốc với ID {drugId} trong DrugService. Status: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Error content: {errorContent}");
                    
                    // Thử lấy danh sách tất cả thuốc để debug
                    try
                    {
                        var allDrugsResponse = await httpClient.GetAsync($"{drugServiceUrl}/api/drugs");
                        if (allDrugsResponse.IsSuccessStatusCode)
                        {
                            var allDrugs = await allDrugsResponse.Content.ReadFromJsonAsync<List<DrugDto>>();
                            _logger.LogWarning($"Danh sách thuốc hiện có trong DrugService: {string.Join(", ", allDrugs?.Select(d => $"ID={d.Id}, Name={d.Name}") ?? new List<string>())}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Không thể lấy danh sách thuốc để debug: {ex.Message}");
                    }
                    
                    return null;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning($"Không có quyền truy cập DrugService. Status: {response.StatusCode}. Có thể service token không hợp lệ.");
                    return null;
                }
                else
                {
                    _logger.LogWarning($"DrugService trả về status: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Error content: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi DrugService: {ex.Message}");
                return null;
            }
        }
    }
}

