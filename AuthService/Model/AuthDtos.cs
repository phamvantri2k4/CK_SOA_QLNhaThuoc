using System.ComponentModel.DataAnnotations;

namespace AuthService.Model
{
    public class UserLoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

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

    public class UserResponseDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class UserUpdateDto
    {
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }

        // Không có [Required], cho phép null
        public string? Password { get; set; }
    }
}
