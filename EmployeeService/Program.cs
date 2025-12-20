using Microsoft.EntityFrameworkCore;
using EmployeeService.Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Shared;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<EmployeeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

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

                var server = app.Services.GetRequiredService<IServer>();
                var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
                var serviceUrl = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                                 ?? addresses?.FirstOrDefault()
                                 ?? (builder.Configuration["ServiceInfo:Port"] is string p ? $"http://localhost:{p}" : "http://localhost:5050");
                serviceUrl = serviceUrl.TrimEnd('/');

                var discoveryClient = new ServiceDiscoveryClient(registryUrl);
                var serviceInfo = new ServiceInfo
                {
                    ServiceName = "EmployeeService",
                    Url = serviceUrl,
                    Description = "Quản lý nhân viên",
                    Version = "1.0"
                };

                var result = await discoveryClient.RegisterServiceAsync(serviceInfo);
                if (result)
                {
                    Console.WriteLine($"✓ EmployeeService đã đăng ký thành công với ServiceRegistry tại {registryUrl}");

                    _ = Task.Run(async () =>
                    {
                        var intervalSeconds = builder.Configuration.GetValue<int?>("ServiceInfo:HeartbeatSeconds") ?? 20;
                        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), app.Lifetime.ApplicationStopping);
                                await discoveryClient.SendHeartbeatAsync("EmployeeService");
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

                Console.WriteLine($"✗ Lần thử {attempt}: EmployeeService không thể đăng ký với ServiceRegistry.");
                Console.WriteLine($"  Đợi {delaySeconds} giây trước khi thử lại...");
                await Task.Delay(delaySeconds * 1000);
            }
            catch (Exception ex)
            {
                attempt++;
                Console.WriteLine($"✗ Lần thử {attempt}: Lỗi khi đăng ký EmployeeService: {ex.Message}");
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
