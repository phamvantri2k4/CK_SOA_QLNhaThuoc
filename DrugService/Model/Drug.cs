namespace DrugService.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    public class Drug
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ImportPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SellPrice { get; set; }
    }


}
