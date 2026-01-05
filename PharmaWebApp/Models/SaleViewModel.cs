namespace PharmaWebApp.Models
{
    /// <summary>
    /// ViewModel để tạo hóa đơn mới
    /// </summary>
    public class CreateSaleViewModel
    {
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public int StaffId { get; set; } = 1;
        public List<SaleItemViewModel> Items { get; set; } = new();
    }

    /// <summary>
    /// ViewModel cho từng item trong hóa đơn
    /// </summary>
    public class SaleItemViewModel
    {
        public int DrugId { get; set; }
        public string UnitType { get; set; } = "pill";
        public int Quantity { get; set; }
    }

    /// <summary>
    /// ViewModel để hiển thị hóa đơn
    /// </summary>
    public class SaleInvoiceDisplayViewModel
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CustomerId { get; set; }  // <--- Added this
        public string? CustomerName { get; set; }
        public int StaffId { get; set; }
        public string? StaffName { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public DateTime? PaidAt { get; set; }
        public List<SaleItemDisplayViewModel> Items { get; set; } = new();
    }

    public class EditSaleViewModel
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public List<SaleItemViewModel> Items { get; set; } = new();
    }

    public class UpdateSaleRequestDto
    {
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public List<SaleItemViewModel> Items { get; set; } = new();
    }

    /// <summary>
    /// ViewModel để hiển thị chi tiết từng item trong hóa đơn
    /// </summary>
    public class SaleItemDisplayViewModel
    {
        public int DrugId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public string UnitType { get; set; } = "pill";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}

