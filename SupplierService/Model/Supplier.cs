using System.ComponentModel.DataAnnotations.Schema;

namespace SupplierService.Models
{
    public class CreatePurchaseOrderRequest
    {
        public int SupplierId { get; set; }
        public List<CreatePurchaseOrderDetailRequest> Details { get; set; } = new();
    }

    public class CreatePurchaseOrderDetailRequest
    {
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
    }

    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }

    public class PurchaseOrder
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseOrderDetail
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
    }

}
