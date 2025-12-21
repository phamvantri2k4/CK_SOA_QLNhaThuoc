using AuthService.Data;
using AuthService.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AuthDbContext _db;

        public UsersController(AuthDbContext db)
        {
            _db = db;
        }

        /* ================= GET ALL ================= */
        // Owner hoặc Service dùng (SOA)
        [HttpGet]
        [Authorize(Roles = "Owner,Service")]
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
                })
                .ToListAsync();

            return Ok(users);
        }

        /* ================= GET BY ID ================= */
        // Owner, Service hoặc chính user
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (!IsOwnerOrService() && GetUserId() != id)
                return Forbid();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            return Ok(ToUserDto(user));
        }

        /* ================= CREATE ================= */
        // Owner tạo tài khoản cho nhân viên
        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Create(UserRegisterDto dto)
        {
            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest(new { message = "Username already exists" });

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

            return CreatedAtAction(nameof(GetById),
                new { id = user.Id },
                ToUserDto(user));
        }

        /* ================= UPDATE ================= */
        // Owner sửa tất cả | User sửa chính mình (không đổi role)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UserUpdateDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            bool isOwner = User.IsInRole("Owner");
            if (!isOwner && GetUserId() != id)
                return Forbid();

            if (!isOwner && dto.Role != user.Role)
                return BadRequest(new { message = "Không có quyền đổi vai trò" });

            user.FullName = dto.FullName;

            if (isOwner)
            {
                user.Role = dto.Role;
                user.IsActive = dto.IsActive;
            }

            if (!string.IsNullOrWhiteSpace(dto.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        /* ================= DELETE ================= */
        // Owner only
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

        /* ================= HELPER ================= */

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        }

        private bool IsOwnerOrService()
        {
            return User.IsInRole("Owner") || User.IsInRole("Service");
        }

        private static UserResponseDto ToUserDto(User u)
        {
            return new UserResponseDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive
            };
        }
    }
}
