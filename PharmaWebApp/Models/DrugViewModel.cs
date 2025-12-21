namespace PharmaWebApp.Models
{
    /// <summary>
    /// ViewModel để hiển thị thông tin thuốc từ DrugService
    /// </summary>
    public class DrugViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int PackSize { get; set; } = 1;
        public int ImportPrice { get; set; }
        public int BoxPrice { get; set; }
        public int SellPricePerPill { get; set; }
        public string? ImageUrl { get; set; }
    }

    /// <summary>
    /// ViewModel để nhập thông tin thuốc mới
    /// </summary>
    public class CreateDrugViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int PackSize { get; set; } = 1;
        public int ImportPrice { get; set; }
        public int BoxPrice { get; set; }
        public int SellPricePerPill { get; set; }
        public string? ImageUrl { get; set; }
        
        // File upload từ máy (không bắt buộc khi edit)
        public IFormFile? ImageFile { get; set; }
    }
}

