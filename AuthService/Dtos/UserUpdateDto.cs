namespace AuthService.Dtos
{
    public class UserUpdateDto
    {
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }

        // Không có [Required], cho phép null
        public string? Password { get; set; }
    }
}