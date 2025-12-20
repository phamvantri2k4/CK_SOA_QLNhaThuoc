using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DrugService.Data;
using Shared;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Cấu hình Database Context - dùng SQL Server
builder.Services.AddDbContext<DrugDbContext>(options =>
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

// HttpClient để gọi ServiceRegistry
builder.Services.AddHttpClient();

var app = builder.Build();

// Seed data: Tạo một số thuốc mẫu nếu database trống
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DrugDbContext>();
    try
    {
        await db.Database.MigrateAsync();

        // Kiểm tra xem đã có thuốc nào chưa
        if (!await db.Drugs.AnyAsync())
        {
            var sampleDrugs = new List<DrugService.Models.Drug>
            {
                new DrugService.Models.Drug
                {
                    Code = "THUOC001",
                    Name = "Paracetamol 500mg",
                    Category = "Giảm đau, Hạ sốt",
                    Unit = "Viên",
                    PackSize = 10,
                    ImportPrice = 5000,
                    SellPrice = 8000,
                    BoxPrice = 75000,
                    ImageUrl = ""
                },
                new DrugService.Models.Drug
                {
                    Code = "THUOC002",
                    Name = "Amoxicillin 250mg",
                    Category = "Kháng sinh",
                    Unit = "Viên",
                    PackSize = 12,
                    ImportPrice = 12000,
                    SellPrice = 18000,
                    BoxPrice = 200000,
                    ImageUrl = ""
                },
                new DrugService.Models.Drug
                {
                    Code = "THUOC003",
                    Name = "Vitamin C 1000mg",
                    Category = "Vitamin",
                    Unit = "Viên",
                    PackSize = 30,
                    ImportPrice = 8000,
                    SellPrice = 12000,
                    BoxPrice = 330000,
                    ImageUrl = ""
                },
                new DrugService.Models.Drug
                {
                    Code = "THUOC004",
                    Name = "Ibuprofen 400mg",
                    Category = "Giảm đau, Chống viêm",
                    Unit = "Viên",
                    PackSize = 10,
                    ImportPrice = 10000,
                    SellPrice = 15000,
                    BoxPrice = 145000,
                    ImageUrl = ""
                },
                new DrugService.Models.Drug
                {
                    Code = "THUOC005",
                    Name = "Omeprazole 20mg",
                    Category = "Dạ dày",
                    Unit = "Viên",
                    PackSize = 14,
                    ImportPrice = 15000,
                    SellPrice = 22000,
                    BoxPrice = 290000,
                    ImageUrl = ""
                }
            };

            db.Drugs.AddRange(sampleDrugs);
            await db.SaveChangesAsync();
            Console.WriteLine($"✓ Đã tạo {sampleDrugs.Count} thuốc mẫu trong DrugService");
            foreach (var drug in sampleDrugs)
            {
                Console.WriteLine($"  - ID: {drug.Id}, Code: {drug.Code}, Name: {drug.Name}");
            }
        }

        if (!await db.Categories.AnyAsync())
        {
            var categoryNames = await db.Drugs
                .Select(d => d.Category)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var categories = categoryNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new DrugService.Models.Category { Name = x.Trim() })
                .ToList();

            if (categories.Count > 0)
            {
                db.Categories.AddRange(categories);
                await db.SaveChangesAsync();
                Console.WriteLine($"✓ Đã tạo {categories.Count} danh mục trong DrugService");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Lỗi khi seed data DrugService: {ex.Message}");
    }
}

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
// Đây là phần quan trọng của kiến trúc SOA: Service Provider tự Publish
Task.Run(async () =>
{
    const int maxRetries = 5;
    const int delaySeconds = 3;
    
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            // Đợi để ứng dụng khởi động hoàn tất
            await Task.Delay(2000);

            // Lấy cấu hình từ appsettings.json
            var registryUrl = builder.Configuration["ServiceRegistry:BaseUrl"] ?? "http://localhost:6000";

            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            var serviceUrl = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                             ?? addresses?.FirstOrDefault()
                             ?? (builder.Configuration["ServiceInfo:Port"] is string p ? $"http://localhost:{p}" : "http://localhost:5001");
            serviceUrl = serviceUrl.TrimEnd('/');

            Console.WriteLine($"[DrugService] Đang thử đăng ký với ServiceRegistry (Lần {attempt}/{maxRetries})...");
            Console.WriteLine($"[DrugService] Registry URL: {registryUrl}");
            Console.WriteLine($"[DrugService] Service URL: {serviceUrl}");

            // Tạo client để gọi ServiceRegistry
            var discoveryClient = new ServiceDiscoveryClient(registryUrl);

            // Thông tin service cần đăng ký
            var serviceInfo = new ServiceInfo
            {
                ServiceName = "DrugService",
                Url = serviceUrl,
                Description = "Quản lý thông tin thuốc",
                Version = "1.0"
            };

            // Gọi API đăng ký
            var result = await discoveryClient.RegisterServiceAsync(serviceInfo);

            if (result)
            {
                Console.WriteLine($"✓ DrugService đã đăng ký thành công với ServiceRegistry tại {registryUrl}");

                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    _ = Task.Run(async () =>
                    {
                        await discoveryClient.UnregisterServiceAsync("DrugService");
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
                            await discoveryClient.SendHeartbeatAsync("DrugService");
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
                return; // Thành công, thoát khỏi vòng lặp
            }
            else
            {
                Console.WriteLine($"✗ Lần thử {attempt}/{maxRetries}: DrugService không thể đăng ký với ServiceRegistry.");
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"  Đợi {delaySeconds} giây trước khi thử lại...");
                    await Task.Delay(delaySeconds * 1000);
                }
                else
                {
                    Console.WriteLine($"✗ DrugService không thể đăng ký sau {maxRetries} lần thử.");
                    Console.WriteLine($"  Vui lòng kiểm tra:");
                    Console.WriteLine($"  1. ServiceRegistry đã chạy chưa? (http://localhost:6000)");
                    Console.WriteLine($"  2. ServiceRegistry có đang lắng nghe trên port 6000 không?");
                    Console.WriteLine($"  3. Firewall có chặn kết nối không?");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Lần thử {attempt}/{maxRetries}: Lỗi khi đăng ký DrugService: {ex.Message}");
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

app.Run();
