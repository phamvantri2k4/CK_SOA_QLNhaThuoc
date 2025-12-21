using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using ReportingService.Data;
using ReportingService.Models;
using Shared;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<ReportingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// HttpClient để gọi ServiceRegistry và SaleService
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Tắt Swagger để đúng kiến trúc SOA và tránh tự mở swagger trên trình duyệt

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// ===== TỰ ĐỘNG ĐĂNG KÝ VỚI SERVICE REGISTRY KHI KHỞI ĐỘNG =====
app.Lifetime.ApplicationStarted.Register(() =>
{
Task.Run(async () =>
{
    const int maxRetries = 5;
    const int delaySeconds = 3;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await Task.Delay(2000);

            var registryUrl = builder.Configuration["ServiceRegistry:BaseUrl"] ?? "http://localhost:6000";

            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            var serviceUrl = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                             ?? addresses?.FirstOrDefault()
                             ?? (builder.Configuration["ServiceInfo:Port"] is string p ? $"http://localhost:{p}" : "http://localhost:5277");
            serviceUrl = serviceUrl.TrimEnd('/');

            Console.WriteLine($"[ReportingService] Đang thử đăng ký với ServiceRegistry (Lần {attempt}/{maxRetries})...");
            Console.WriteLine($"[ReportingService] Registry URL: {registryUrl}");
            Console.WriteLine($"[ReportingService] Service URL: {serviceUrl}");

            var discoveryClient = new ServiceDiscoveryClient(registryUrl);

            var serviceInfo = new ServiceInfo
            {
                ServiceName = "ReportingService",
                Url = serviceUrl,
                Description = "Báo cáo doanh thu / thống kê",
                Version = "1.0"
            };

            var result = await discoveryClient.RegisterServiceAsync(serviceInfo);
            if (result)
            {
                Console.WriteLine($"✓ ReportingService đã đăng ký thành công với ServiceRegistry tại {registryUrl}");

                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    _ = Task.Run(async () =>
                    {
                        await discoveryClient.UnregisterServiceAsync("ReportingService");
                    });
                });

                _ = Task.Run(async () =>
                {
                    var intervalSeconds = builder.Configuration.GetValue<int?>("ServiceInfo:HeartbeatSeconds") ?? 20;
                    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), app.Lifetime.ApplicationStopping);
                            await discoveryClient.SendHeartbeatAsync("ReportingService");
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch
                        {
                        }
                    }
                });
                return;
            }

            Console.WriteLine($"✗ Lần thử {attempt}/{maxRetries}: ReportingService không thể đăng ký với ServiceRegistry.");
            if (attempt < maxRetries)
            {
                Console.WriteLine($"  Đợi {delaySeconds} giây trước khi thử lại...");
                await Task.Delay(delaySeconds * 1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Lần thử {attempt}/{maxRetries}: Lỗi khi đăng ký ReportingService: {ex.Message}");
            if (attempt < maxRetries)
            {
                Console.WriteLine($"  Đợi {delaySeconds} giây trước khi thử lại...");
                await Task.Delay(delaySeconds * 1000);
            }
        }
    }
});
});

app.Run();
