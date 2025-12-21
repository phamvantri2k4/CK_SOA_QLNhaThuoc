using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// BaseController: cung cấp các hàm dùng chung cho controller phía client
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// Lấy JWT token đã lưu trong Claim khi đăng nhập
        /// </summary>
        protected string? GetJwtToken()
            => User?.FindFirst("Token")?.Value;

        /// <summary>
        /// Tạo HttpClient có gắn JWT token (dùng khi gọi API cần đăng nhập)
        /// </summary>
        protected HttpClient CreateAuthenticatedHttpClient()
        {
            var factory = HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>();

            var client = factory.CreateClient();

            var token = GetJwtToken();
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        /// <summary>
        /// Tạo HttpClient thường (không cần JWT)
        /// </summary>
        protected HttpClient CreateHttpClient()
        {
            var factory = HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>();

            return factory.CreateClient();
        }
    }
}
