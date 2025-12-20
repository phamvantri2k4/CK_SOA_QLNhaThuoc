namespace PharmaWebApp.Models
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
}
