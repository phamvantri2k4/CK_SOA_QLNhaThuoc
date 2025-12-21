using AuthService.Data;
using AuthService.Model;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthDbContext _db;
        private readonly IConfiguration _config;

        public AuthController(AuthDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        /* ================= REGISTER ================= */

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto dto)
        {
            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest(new { message = "Username already exists" });

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Role = "Staff",
                IsActive = true
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(
                nameof(UsersController.GetById),
                "Users",
                new { id = user.Id },
                ToUserResponse(user)
            );
        }

        /* ================= LOGIN ================= */

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginDto dto)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);

            if (user == null || !user.IsActive)
                return Unauthorized(new { message = "Invalid username or password" });

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid username or password" });

            return Ok(new
            {
                token = GenerateJwtToken(user),
                user = ToUserResponse(user)
            });
        }

        /* ================= SERVICE TOKEN ================= */

        /// <summary>
        /// Dùng cho service gọi service (SOA internal)
        /// </summary>
        [HttpPost("service-token")]
        public IActionResult GetServiceToken(ServiceTokenRequest req)
        {
            var serviceKey = _config["ServiceKey"];
            if (req.ServiceKey != serviceKey)
                return Unauthorized(new { message = "Invalid service key" });

            var serviceUser = new User
            {
                Id = 0,
                Username = "ServiceAccount",
                FullName = "Service Account",
                Role = "Service"
            };

            return Ok(new { token = GenerateJwtToken(serviceUser) });
        }

        /* ================= HELPER ================= */

        private string GenerateJwtToken(User user)
        {
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName ?? "")
            };

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(jwt["ExpireMinutes"]!)),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static UserResponseDto ToUserResponse(User u)
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

    public class ServiceTokenRequest
    {
        public string ServiceKey { get; set; } = string.Empty;
    }
}
