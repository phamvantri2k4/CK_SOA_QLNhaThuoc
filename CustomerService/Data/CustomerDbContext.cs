using CustomerService.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Data;

public class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; } = null!;
}

