using SaleService.Model;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace SaleService.Data
{
    public class SaleDbContext : DbContext
    {
        public SaleDbContext(DbContextOptions<SaleDbContext> options) : base(options)
        {
        }

        public DbSet<SaleInvoice> SaleInvoices { get; set; } = null!;
        public DbSet<SaleInvoiceDetail> SaleInvoiceDetails { get; set; } = null!;
    }
}
