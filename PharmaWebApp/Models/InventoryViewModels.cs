namespace PharmaWebApp.Models
{
    public class InventoryItemViewModel
    {
        public int Id { get; set; }
        public int DrugId { get; set; }
        public string? DrugName { get; set; }
        public int Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public int PackSize { get; set; } = 1;

        public decimal? ImportPrice { get; set; }
        public string? ImportSupplierName { get; set; }
        public DateTime? ImportCreatedAt { get; set; }
        public decimal? SellPrice { get; set; }
        public decimal? BoxPrice { get; set; }
    }

    public class LatestImportPriceViewModel
    {
        public int DrugId { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
    }

    public class InventoryLowStockWarningViewModel
    {
        public int DrugId { get; set; }
        public string? DrugName { get; set; }
        public int TotalQuantity { get; set; }
        public int PackSize { get; set; } = 1;
    }
}
