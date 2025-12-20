using System.ComponentModel.DataAnnotations;

namespace PharmaWebApp.Models
{
    public class EmployeeListItemViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateEmployeeViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Staff";

        public bool IsActive { get; set; } = true;
    }

    public class EditEmployeeViewModel
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Staff";

        public bool IsActive { get; set; } = true;

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }
    }
}
