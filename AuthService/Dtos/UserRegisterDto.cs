using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos
{
    public class UserRegisterDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff"; // default Staff

        public bool IsActive { get; set; } = true;
    }
}
