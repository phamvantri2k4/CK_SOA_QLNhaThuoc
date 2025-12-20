using AuthService.Data;
using AuthService.Dtos;
using AuthService.Model;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto dto)
        {
            // kiểm tra tồn tại username
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

            var res = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive
            };

            // Trỏ chính xác tới UsersController và method GetById
            return CreatedAtAction(
                actionName: "GetById",      // tên method trong UsersController
                controllerName: "Users",    // tên controller (bỏ "Controller")
                routeValues: new { id = user.Id },
                value: res
            );
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginDto dto)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null) return Unauthorized(new { message = "Invalid username or password" });
            if (!user.IsActive) return Unauthorized(new { message = "Account not active" });

            bool valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!valid) return Unauthorized(new { message = "Invalid username or password" });

            var token = GenerateJwtToken(user);
            return Ok(new { token, user = new { id = user.Id, username = user.Username, fullName = user.FullName, role = user.Role } });
        }

        /// <summary>
        /// Tạo service token cho các service gọi lẫn nhau
        /// POST /api/auth/service-token
        /// </summary>
        [HttpPost("service-token")]
        public IActionResult GetServiceToken([FromBody] ServiceTokenRequest request)
        {
            // Kiểm tra service key (có thể lưu trong config)
            var validServiceKey = _config["ServiceKey"] ?? "ServiceKey123!";
            if (request.ServiceKey != validServiceKey)
            {
                return Unauthorized(new { message = "Invalid service key" });
            }

            // Tạo service user token
            var serviceUser = new User
            {
                Id = 0,
                Username = "ServiceAccount",
                Role = "Service",
                FullName = "Service Account"
            };

            var token = GenerateJwtToken(serviceUser);
            return Ok(new { token });
        }

        private string GenerateJwtToken(User user)
        {
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName)
            };

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(jwt["ExpireMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class ServiceTokenRequest
    {
        public string ServiceKey { get; set; } = string.Empty;
    }
}
