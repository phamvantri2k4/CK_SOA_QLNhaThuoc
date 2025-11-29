using EmployeeService.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Data;

public class EmployeeDbContext : DbContext
{
    public EmployeeDbContext(DbContextOptions<EmployeeDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; } = null!;
}

