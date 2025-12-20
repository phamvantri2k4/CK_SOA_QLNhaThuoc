namespace DrugService.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    public class Drug
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        public int PackSize { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ImportPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SellPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BoxPrice { get; set; }

        public string? ImageUrl { get; set; }
    }


}
