using AuthService.Data;
using AuthService.Dtos;
using AuthService.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // yêu cầu xác thực cho tất cả endpoint trong controller này
    public class UsersController : ControllerBase
    {
        private readonly AuthDbContext _db;

        public UsersController(AuthDbContext db)
        {
            _db = db;
        }

        // Owner hoặc Service: lấy danh sách users
        [HttpGet]
        [Authorize(Roles = "Owner,Service")] // Owner (admin) hoặc Service có quyền xem danh sách
        public async Task<IActionResult> GetAll()
        {
            var users = await _db.Users
                .Select(u => new UserResponseDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Role = u.Role,
                    IsActive = u.IsActive
                }).ToListAsync();

            return Ok(users);
        }

        // Owner, Service hoặc chính bản thân user có thể xem chi tiết
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            // allow Owner, Service (for inter-service calls) or the user themself
            var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var currentRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            // Cho phép Service role (để các service khác có thể lấy thông tin user)
            if (currentRole != "Owner" && currentRole != "Service" && currentUserId != id)
                return Forbid();

            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            return Ok(new UserResponseDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive
            });
        }

        // Owner only: tạo user (admin tạo tài khoản cho nhân viên)
        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Create(UserRegisterDto dto)
        {
            if (await _db.Users.AnyAsync(x => x.Username == dto.Username))
                return BadRequest(new { message = "Username exists" });

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Role = dto.Role,
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive
            });
        }

        // Update: Owner can update any; user can update own profile (except role)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UserUpdateDto dto)
        {
            // Lấy ID người đang đăng nhập
            var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Tìm user cần sửa trong DB
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Kiểm tra quyền: Chỉ Owner HOẶC chính chủ mới được sửa
            // Lưu ý: User.IsInRole("Owner") sẽ tự động kiểm tra role chính xác nhất
            bool isOwner = User.IsInRole("Owner");

            if (!isOwner && currentUserId != id)
                return Forbid();

            // Nếu không phải Owner mà cố tình đổi Role -> Báo lỗi
            if (!isOwner && dto.Role != user.Role)
                return BadRequest(new { message = "Bạn không có quyền thay đổi vai trò." });

            // --- BẮT ĐẦU CẬP NHẬT ---
            user.FullName = dto.FullName;

            // Cập nhật trạng thái (Chỉ Owner mới được khóa/mở tài khoản)
            if (isOwner)
            {
                user.IsActive = dto.IsActive;
            }

            // Cập nhật mật khẩu (chỉ nếu có nhập pass mới)
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            // Cập nhật Role (Chỉ Owner mới được đổi)
            // Lỗi cũ của bạn nằm ở đây (do check sai quyền nên dòng này bị bỏ qua)
            if (isOwner)
            {
                user.Role = dto.Role;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Delete: Owner only
        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
