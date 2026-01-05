using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    public class AuthController : BaseController
    {
        private readonly ILogger<AuthController> _logger;
        private readonly ServiceUrlHelper _serviceUrl;

        public AuthController(ILogger<AuthController> logger, ServiceUrlHelper serviceUrl)
        {
            _logger = logger;
            _serviceUrl = serviceUrl;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var authServiceUrl = await _serviceUrl.GetAuthServiceUrlAsync();
                var httpClient = CreateHttpClient();

                var loginRequest = new
                {
                    username = model.Username,
                    password = model.Password
                };

                var response = await httpClient.PostAsJsonAsync($"{authServiceUrl}/api/auth/login", loginRequest);

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng");
                    return View(model);
                }

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var token = result.GetProperty("token").GetString();

                if (string.IsNullOrEmpty(token))
                {
                    ModelState.AddModelError("", "Không thể đăng nhập");
                    return View(model);
                }

                // Lấy thông tin user từ token hoặc gọi API
                var userInfo = await GetUserInfoFromTokenAsync(token);

                if (userInfo == null)
                {
                    ModelState.AddModelError("", "Không thể lấy thông tin người dùng");
                    return View(model);
                }

                // Tạo claims và đăng nhập
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userInfo.Id.ToString()),
                    new Claim(ClaimTypes.Name, userInfo.Username),
                    new Claim(ClaimTypes.Role, userInfo.Role),
                    new Claim("FullName", userInfo.FullName),
                    new Claim("Token", token) // Lưu token để gọi API
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation($"User {model.Username} đã đăng nhập thành công");

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi đăng nhập: {ex.Message}");
                ModelState.AddModelError("", "Đã xảy ra lỗi khi đăng nhập");
                return View(model);
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User đã đăng xuất");
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var authServiceUrl = await _serviceUrl.GetAuthServiceUrlAsync();
                var httpClient = CreateHttpClient();

                var registerRequest = new
                {
                    username = model.Username,
                    password = model.Password,
                    fullName = model.FullName
                };

                var response = await httpClient.PostAsJsonAsync($"{authServiceUrl}/api/auth/register", registerRequest);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent);
                    var errorMessage = errorJson.TryGetProperty("message", out var msg) ? msg.GetString() : "Đăng ký thất bại";
                    
                    ModelState.AddModelError("", errorMessage ?? "Đăng ký thất bại");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi đăng ký: {ex.Message}");
                ModelState.AddModelError("", "Đã xảy ra lỗi khi đăng ký");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task<UserInfo?> GetUserInfoFromTokenAsync(string token)
        {
            try
            {
                var authServiceUrl = await _serviceUrl.GetAuthServiceUrlAsync();
                var httpClient = CreateHttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Lấy user ID từ token (parse JWT)
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(token);
                var userId = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return null;
                }

                var response = await httpClient.GetAsync($"{authServiceUrl}/api/users/{userId}");

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<UserInfo>();
                    return user;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}

