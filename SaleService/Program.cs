using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SaleService.Data;
using Shared;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Cấu hình Database Context - dùng SQL Server
builder.Services.AddDbContext<SaleDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// HttpClient để gọi các service khác (DrugService, ServiceRegistry)
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Tắt Swagger để đơn giản hóa
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseAuthentication();
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
                             ?? (builder.Configuration["ServiceInfo:Port"] is string p ? $"http://localhost:{p}" : "http://localhost:5002");
            serviceUrl = serviceUrl.TrimEnd('/');

            Console.WriteLine($"[SaleService] Đang thử đăng ký với ServiceRegistry (Lần {attempt}/{maxRetries})...");
            Console.WriteLine($"[SaleService] Registry URL: {registryUrl}");
            Console.WriteLine($"[SaleService] Service URL: {serviceUrl}");

            var discoveryClient = new ServiceDiscoveryClient(registryUrl);
            var serviceInfo = new ServiceInfo
            {
                ServiceName = "SaleService",
                Url = serviceUrl,
                Description = "Quản lý hóa đơn bán hàng",
                Version = "1.0"
            };

            var result = await discoveryClient.RegisterServiceAsync(serviceInfo);
            if (result)
            {
                Console.WriteLine($"✓ SaleService đã đăng ký thành công với ServiceRegistry tại {registryUrl}");

                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    _ = Task.Run(async () =>
                    {
                        await discoveryClient.UnregisterServiceAsync("SaleService");
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
                            await discoveryClient.SendHeartbeatAsync("SaleService");
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

            Console.WriteLine($"✗ Lần thử {attempt}/{maxRetries}: SaleService không thể đăng ký với ServiceRegistry.");
            if (attempt < maxRetries)
            {
                Console.WriteLine($"  Đợi {delaySeconds} giây trước khi thử lại...");
                await Task.Delay(delaySeconds * 1000);
            }
            else
            {
                Console.WriteLine($"✗ SaleService không thể đăng ký sau {maxRetries} lần thử.");
                Console.WriteLine($"  Vui lòng kiểm tra:");
                Console.WriteLine($"  1. ServiceRegistry đã chạy chưa? (http://localhost:6000)");
                Console.WriteLine($"  2. ServiceRegistry có đang lắng nghe trên port 6000 không?");
                Console.WriteLine($"  3. Firewall có chặn kết nối không?");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Lần thử {attempt}/{maxRetries}: Lỗi khi đăng ký SaleService: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner exception: {ex.InnerException.Message}");
            }

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
