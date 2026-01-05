namespace InventoryService.Models
{
    public class ImportStockRequest
    {
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public string UnitType { get; set; } = "box"; // "box" hoặc "pill"
        public DateTime? ExpiryDate { get; set; }
    }

    public class ExportStockRequest
    {
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public string UnitType { get; set; } = "pill"; // Mặc định xuất theo viên
    }

    public class InventoryItem
    {
        public int Id { get; set; }
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public string UnitType { get; set; } = "box"; // Đơn vị lưu trữ: "box" hoặc "pill"
        public DateTime? ExpiryDate { get; set; }
    }

    public class StockTransaction
    {
        public int Id { get; set; }
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public string UnitType { get; set; } = "box";
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
