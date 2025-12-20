using System.ComponentModel.DataAnnotations;

namespace PharmaWebApp.Models.ViewModels
{
    public class ServiceInfoViewModel
    {
        public string ServiceName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
    }

    public class DrugItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal SellPrice { get; set; }
    }

    public class CreateInvoiceInputModel
    {
        [Display(Name = "Mã khách hàng (tùy chọn)")]
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã nhân viên.")]
        [Display(Name = "Mã nhân viên")]
        public int StaffId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã thuốc.")]
        [Display(Name = "Mã thuốc")]
        public int DrugId { get; set; }

        [Range(1, 1000, ErrorMessage = "Số lượng phải từ 1 đến 1000.")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Đơn giá (để trống để dùng giá hệ thống)")]
        public decimal UnitPrice { get; set; }
    }

    public class SaleInvoiceDetailViewModel
    {
        public int DrugId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class SaleInvoiceViewModel
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CustomerId { get; set; }
        public int StaffId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<SaleInvoiceDetailViewModel> Details { get; set; } = new();
    }

    public class HomeViewModel
    {
        public List<ServiceInfoViewModel> Services { get; set; } = new();
        public List<DrugItemViewModel> Drugs { get; set; } = new();

        public string? SearchName { get; set; }
        public string? SearchCode { get; set; }
        public string? StatusMessage { get; set; }

        public CreateInvoiceInputModel InvoiceForm { get; set; } = new();
        public string? InvoiceStatus { get; set; }
        public SaleInvoiceViewModel? LastInvoice { get; set; }
    }
}

