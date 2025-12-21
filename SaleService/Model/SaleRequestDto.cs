namespace SaleService.Model
{
    /// <summary>
    /// DTO để nhận request khi tạo hóa đơn mới
    /// </summary>
    public class CreateSaleRequest
    {
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public int StaffId { get; set; }
        public List<SaleItemRequest> Items { get; set; } = new();
    }

    public class UpdateSaleRequest
    {
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public List<SaleItemRequest> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO cho từng item trong hóa đơn
    /// </summary>
    public class SaleItemRequest
    {
        public int DrugId { get; set; }
        public string UnitType { get; set; } = "pill";
        public int Quantity { get; set; }
    }

    /// <summary>
    /// DTO để trả về thông tin hóa đơn chi tiết
    /// </summary>
    public class SaleInvoiceResponse
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int StaffId { get; set; }
        public string? StaffName { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public DateTime? PaidAt { get; set; }
        public List<SaleItemResponse> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO cho item trong response
    /// </summary>
    public class SaleItemResponse
    {
        public int DrugId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public string UnitType { get; set; } = "pill";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    /// <summary>
    /// DTO để nhận thông tin thuốc từ DrugService
    /// </summary>
    public class DrugDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int PackSize { get; set; } = 1;
        public decimal ImportPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal BoxPrice { get; set; }
    }

    /// <summary>
    /// DTO để nhận thông tin khách hàng từ CustomerService
    /// </summary>
    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO để nhận thông tin user từ AuthService
    /// </summary>
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
