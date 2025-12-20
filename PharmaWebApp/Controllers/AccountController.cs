using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;

namespace PharmaWebApp.Controllers
{
    [Authorize]
    public class AccountController : BaseController
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IServiceResolver _serviceResolver;

        public AccountController(ILogger<AccountController> logger, IServiceResolver serviceResolver)
        {
            _logger = logger;
            _serviceResolver = serviceResolver;
        }

        public async Task<IActionResult> Profile()
        {
            var vm = new AccountProfileViewModel();

            try
            {
                var idClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _ = int.TryParse(idClaim, out var userId);

                vm.Id = userId;
                vm.Username = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
                vm.FullName = User?.FindFirst("FullName")?.Value ?? string.Empty;
                vm.Role = User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

                var authServiceUrl = await _serviceResolver.GetRequiredAsync("AuthService");
                var httpClient = CreateAuthenticatedHttpClient();

                if (userId > 0)
                {
                    var response = await httpClient.GetAsync($"{authServiceUrl}/api/users/{userId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var user = await response.Content.ReadFromJsonAsync<AuthUserDto>();
                        if (user != null)
                        {
                            vm.Username = user.Username;
                            vm.FullName = user.FullName;
                            vm.Role = user.Role;
                            vm.IsActive = user.IsActive;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Không thể load thông tin tài khoản: {ex.Message}");
            }

            return View(vm);
        }

        private class AuthUserDto
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }
    }
}
