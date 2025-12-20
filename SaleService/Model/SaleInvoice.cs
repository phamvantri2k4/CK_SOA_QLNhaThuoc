using System.ComponentModel.DataAnnotations.Schema;

namespace SaleService.Model
{
    public class SaleInvoice
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? CustomerId { get; set; }   // Id từ CustomerService
        public int StaffId { get; set; }       // Id từ UserService

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Trạng thái thanh toán: "Pending" (Chờ thanh toán) hoặc "Paid" (Đã thanh toán)
        /// </summary>
        public string PaymentStatus { get; set; } = "Pending";

        /// <summary>
        /// Ngày thanh toán (nếu đã thanh toán)
        /// </summary>
        public DateTime? PaidAt { get; set; }

        // Navigation property
        public virtual ICollection<SaleInvoiceDetail> Details { get; set; } = new List<SaleInvoiceDetail>();
    }

    public class SaleInvoiceDetail
    {
        public int Id { get; set; }

        public int SaleInvoiceId { get; set; } // Id hóa đơn
        public int DrugId { get; set; }        // Id từ DrugService
        public string UnitType { get; set; } = "pill";
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        // Navigation property
        public virtual SaleInvoice? SaleInvoice { get; set; }
    }
}
