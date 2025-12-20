namespace InventoryService.Models
{
    public class ImportStockRequest
    {
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class ExportStockRequest
    {
        public int DrugId { get; set; }
        public int Quantity { get; set; }
    }

    public class InventoryItem
    {
        public int Id { get; set; }
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class StockTransaction
    {
        public int Id { get; set; }
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
