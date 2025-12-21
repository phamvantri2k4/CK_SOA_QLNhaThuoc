using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaWebApp.Models;
using PharmaWebApp.Services;
using System.Net.Http.Json;
using System.Security.Claims;

namespace PharmaWebApp.Controllers
{
    [Authorize]
    public class AccountController : BaseController
    {
        private readonly IServiceResolver _resolver;

        public AccountController(IServiceResolver resolver)
        {
            _resolver = resolver;
        }

        /* ================= PROFILE ================= */

        public async Task<IActionResult> Profile()
        {
            var vm = new AccountProfileViewModel
            {
                Id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0,
                Username = User.FindFirstValue(ClaimTypes.Name) ?? "",
                FullName = User.FindFirstValue("FullName") ?? "",
                Role = User.FindFirstValue(ClaimTypes.Role) ?? ""
            };

            if (vm.Id > 0)
            {
                var (client, url) = await GetAuthClientAsync();
                var user = await client.GetFromJsonAsync<UserDto>($"{url}/api/users/{vm.Id}");

                if (user != null)
                {
                    vm.Username = user.Username;
                    vm.FullName = user.FullName;
                    vm.Role = user.Role;
                    vm.IsActive = user.IsActive;
                }
            }

            return View(vm);
        }

        /* ================= HÀM DÙNG CHUNG ================= */

        private async Task<(HttpClient client, string url)> GetAuthClientAsync()
        {
            var url = await _resolver.GetRequiredAsync("AuthService");
            return (CreateAuthenticatedHttpClient(), url);
        }

        /* ================= DTO ĐƠN GIẢN ================= */

        private class UserDto
        {
            public int Id { get; set; }
            public string Username { get; set; } = "";
            public string FullName { get; set; } = "";
            public string Role { get; set; } = "";
            public bool IsActive { get; set; }
        }
    }
}
