using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace PharmaWebApp.Controllers
{
    /// <summary>
    /// Base controller với các helper methods chung
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// Lấy JWT token từ claims của user đã đăng nhập
        /// </summary>
        protected string? GetJwtToken()
        {
            return User?.FindFirst("Token")?.Value;
        }

        /// <summary>
        /// Tạo HttpClient với JWT token trong header
        /// </summary>
        protected HttpClient CreateAuthenticatedHttpClient()
        {
            var httpClientFactory = HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();

            var token = GetJwtToken();
            if (!string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            return httpClient;
        }

        protected HttpClient CreateHttpClient()
        {
            var httpClientFactory = HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>();
            return httpClientFactory.CreateClient();
        }
    }
}

