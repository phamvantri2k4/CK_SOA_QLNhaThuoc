using System.Threading.Tasks;

namespace PharmaWebApp.Services
{
    public interface IServiceResolver
    {
        Task<string> GetRequiredAsync(string serviceName);
        Task<string?> GetOptionalAsync(string serviceName);
    }
}
