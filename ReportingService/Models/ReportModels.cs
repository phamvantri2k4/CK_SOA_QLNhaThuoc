namespace ReportingService.Models
{
    public class ReportTopDrugViewModel
    {
        public int DrugId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    public class SalesReportViewModel
    {
        public string ReportType { get; set; } = "day";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int InvoiceCount { get; set; }
        public int PaidCount { get; set; }
        public int PendingCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PaidRevenue { get; set; }
        public List<ReportTopDrugViewModel> TopDrugs { get; set; } = new();
        public List<SaleInvoiceDisplayViewModel> Invoices { get; set; } = new();
        public int? SelectedYear { get; set; }
        public int? SelectedMonth { get; set; }
        public DateTime? SelectedDate { get; set; }
    }

    public class SaleInvoiceDisplayViewModel
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CustomerName { get; set; }
        public int StaffId { get; set; }
        public string? StaffName { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public DateTime? PaidAt { get; set; }
        public List<SaleItemDisplayViewModel> Items { get; set; } = new();
    }

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
