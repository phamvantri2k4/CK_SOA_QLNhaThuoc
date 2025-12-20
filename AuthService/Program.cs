using AuthService.Data;
using AuthService.Model;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<AuthDbContext>(options =>
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed data: Tạo tài khoản admin mặc định nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    try
    {
        // Kiểm tra xem đã có user nào chưa
        if (!await db.Users.AnyAsync())
        {
            var adminUser = new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                FullName = "Quản trị viên",
                Role = "Owner",
                IsActive = true
            };

            db.Users.Add(adminUser);
            await db.SaveChangesAsync();
            Console.WriteLine("✓ Đã tạo tài khoản admin mặc định:");
            Console.WriteLine("  Username: admin");
            Console.WriteLine("  Password: admin123");
            Console.WriteLine("  Role: Owner");
        }
        else
        {
            // Kiểm tra xem đã có Owner chưa
            var hasOwner = await db.Users.AnyAsync(u => u.Role == "Owner");
            if (!hasOwner)
            {
                var adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FullName = "Quản trị viên",
                    Role = "Owner",
                    IsActive = true
                };

                db.Users.Add(adminUser);
                await db.SaveChangesAsync();
                Console.WriteLine("✓ Đã tạo tài khoản admin mặc định:");
                Console.WriteLine("  Username: admin");
                Console.WriteLine("  Password: admin123");
                Console.WriteLine("  Role: Owner");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Lỗi khi seed data: {ex.Message}");
    }
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        try
        {
            await Task.Delay(2000);

            var registryUrl = builder.Configuration["ServiceRegistry:BaseUrl"] ?? "http://localhost:6000";

            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            var serviceUrl = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                             ?? addresses?.FirstOrDefault()
                             ?? (builder.Configuration["ServiceInfo:Port"] is string p ? $"http://localhost:{p}" : "http://localhost:5004");
            serviceUrl = serviceUrl.TrimEnd('/');

            var discoveryClient = new ServiceDiscoveryClient(registryUrl);

            var serviceInfo = new ServiceInfo
            {
                ServiceName = "AuthService",
                Url = serviceUrl,
                Description = "Xác thực và phân quyền người dùng",
                Version = "1.0"
            };

            var result = await discoveryClient.RegisterServiceAsync(serviceInfo);

            if (result)
            {
                Console.WriteLine($"✓ AuthService đã đăng ký thành công với ServiceRegistry tại {registryUrl}");

                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    _ = Task.Run(async () =>
                    {
                        await discoveryClient.UnregisterServiceAsync("AuthService");
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
                            await discoveryClient.SendHeartbeatAsync("AuthService");
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
            }
            else
            {
                Console.WriteLine($"✗ AuthService không thể đăng ký với ServiceRegistry. Vui lòng kiểm tra ServiceRegistry đã chạy chưa.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Lỗi khi đăng ký AuthService: {ex.Message}");
        }
    });
});

app.Run();
