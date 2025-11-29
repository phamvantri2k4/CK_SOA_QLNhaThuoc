using DrugService.Models;
using Microsoft.EntityFrameworkCore;

namespace DrugService.Data;

public class DrugDbContext : DbContext
{
    public DrugDbContext(DbContextOptions<DrugDbContext> options) : base(options)
    {
    }

    public DbSet<Drug> Drugs { get; set; } = null!;
}

