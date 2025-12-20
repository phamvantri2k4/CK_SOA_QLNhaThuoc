using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Shared;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// HttpClient để gọi ServiceRegistry
builder.Services.AddHttpClient();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Comment UseHttpsRedirection để tránh lỗi SSL trong development
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// ===== TỰ ĐỘNG ĐĂNG KÝ VỚI SERVICE REGISTRY KHI KHỞI ĐỘNG =====
app.Lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        const int delaySeconds = 3;

        var attempt = 0;
        while (true)
        {
            try
            {
                attempt++;
                var registryUrl = builder.Configuration["ServiceRegistry:BaseUrl"] ?? "http://localhost:6000";

                // Lấy URL thực tế app đang listen để tránh mismatch port khi chạy bằng VS/launchSettings
                var server = app.Services.GetRequiredService<IServer>();
                var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
                var serviceUrl = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                                 ?? addresses?.FirstOrDefault()
                                 ?? (builder.Configuration["ServiceInfo:Port"] is string p ? $"http://localhost:{p}" : "http://localhost:5006");
                serviceUrl = serviceUrl.TrimEnd('/');

                Console.WriteLine($"[InventoryService] Đang thử đăng ký với ServiceRegistry (Lần {attempt})...");
                Console.WriteLine($"[InventoryService] Registry URL: {registryUrl}");
                Console.WriteLine($"[InventoryService] Service URL (detected): {serviceUrl}");

                var discoveryClient = new ServiceDiscoveryClient(registryUrl);
                var serviceInfo = new ServiceInfo
                {
                    ServiceName = "InventoryService",
                    Url = serviceUrl,
                    Description = "Quản lý tồn kho",
                    Version = "1.0"
                };

                var result = await discoveryClient.RegisterServiceAsync(serviceInfo);
                if (result)
                {
                    Console.WriteLine($"✓ InventoryService đã đăng ký thành công với ServiceRegistry tại {registryUrl}");

                    _ = Task.Run(async () =>
                    {
                        var intervalSeconds = builder.Configuration.GetValue<int?>("ServiceInfo:HeartbeatSeconds") ?? 20;
                        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), app.Lifetime.ApplicationStopping);
                                await discoveryClient.SendHeartbeatAsync("InventoryService");
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

                Console.WriteLine($"✗ Lần thử {attempt}: InventoryService không thể đăng ký với ServiceRegistry.");
                Console.WriteLine($"  Đợi {delaySeconds} giây trước khi thử lại...");
                await Task.Delay(delaySeconds * 1000);
            }
            catch (Exception ex)
            {
                attempt++;
                Console.WriteLine($"✗ Lần thử {attempt}: Lỗi khi đăng ký InventoryService: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                }

                Console.WriteLine($"  Đợi {delaySeconds} giây trước khi thử lại...");
                await Task.Delay(delaySeconds * 1000);
            }
        }
    });
});

app.Run();
