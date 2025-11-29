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
        }

        public class SaleInvoiceDetail
        {
            public int Id { get; set; }

            public int SaleInvoiceId { get; set; } // Id hóa đơn
            public int DrugId { get; set; }        // Id từ DrugService
            public int Quantity { get; set; }

            [Column(TypeName = "decimal(18,2)")]
            public decimal UnitPrice { get; set; }
        }
    }
