namespace EmployeeService.Models
{
    public class Employee
    {
        public int Id { get; set; }

        public string FullName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string? Email { get; set; }
        public string? Address { get; set; }

        public string? Position { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
