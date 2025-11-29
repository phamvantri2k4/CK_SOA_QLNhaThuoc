using Microsoft.EntityFrameworkCore;
using ReportingService.Models;

namespace ReportingService.Data;

public class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options)
    {
    }

    public DbSet<RevenueReport> RevenueReports { get; set; } = null!;
    public DbSet<TopSellingDrug> TopSellingDrugs { get; set; } = null!;
}

