using System.ComponentModel.DataAnnotations.Schema;

namespace ReportingService.Models
{
    public class RevenueReport
    {
        public int Id { get; set; }              
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; }
    }

    public class TopSellingDrug
    {
        public int Id { get; set; }           
        public int DrugId { get; set; }
        public string DrugName { get; set; }
        public int QuantitySold { get; set; }
    }
}
