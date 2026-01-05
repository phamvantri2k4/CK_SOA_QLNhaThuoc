namespace PharmaWebApp.Models
{
    public class SupplierViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class CreateSupplierViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class PurchaseOrderViewModel
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseOrderDetailViewModel
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int DrugId { get; set; }
        public string? DrugName { get; set; }
        public int Quantity { get; set; }
        public string UnitType { get; set; } = "box"; 
        public decimal UnitPrice { get; set; }
    }

    public class PurchaseOrderDetailsResponseViewModel
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PurchaseOrderDetailViewModel> Details { get; set; } = new();
    }

    public class CreatePurchaseOrderViewModel
    {
        public int SupplierId { get; set; }
        public List<CreatePurchaseOrderDetailViewModel> Details { get; set; } = new();
    }

    public class CreatePurchaseOrderDetailViewModel
    {
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public string UnitType { get; set; } = "box"; // "box" hoặc "pill"
        public decimal UnitPrice { get; set; }
        public DateTime? ExpiryDate { get; set; } // Hạn sử dụng của lô hàng
    }
}
