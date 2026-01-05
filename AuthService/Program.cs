using AuthService.Data;
using AuthService.Model;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "AuthService" }));

// ===== ĐĂNG KÝ VÀO CONSUL =====
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        try
        {
            await Task.Delay(1000);

            var consulClient = new Consul.ConsulClient(config =>
            {
                config.Address = new Uri("http://localhost:8500");
            });

            var server = app.Services.GetRequiredService<IServer>();
            var addressFeature = server.Features.Get<IServerAddressesFeature>();
            var address = addressFeature?.Addresses.FirstOrDefault(a => a.StartsWith("http://")) ?? "http://localhost:5004";
            
            var uri = new Uri(address);
            var servicePort = uri.Port;
            var serviceName = "AuthService";
            var serviceId = $"{serviceName}-{Guid.NewGuid()}";

            Console.WriteLine($"🔍 Detected {serviceName} at: {address}");

            var registration = new Consul.AgentServiceRegistration
            {
                ID = serviceId,
                Name = serviceName,
                Address = "localhost",
                Port = servicePort,
                Check = new Consul.AgentServiceCheck
                {
                    HTTP = $"http://localhost:{servicePort}/health",
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(5)
                }
            };

            await consulClient.Agent.ServiceRegister(registration);
            Console.WriteLine($"✅ {serviceName} đã đăng ký vào Consul");

            lifetime.ApplicationStopping.Register(() =>
            {
                consulClient.Agent.ServiceDeregister(serviceId).Wait();
                Console.WriteLine($"🔴 {serviceName} đã hủy đăng ký");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi Consul: {ex.Message}");
        }
    });
});

// Seed data: Tạo tài khoản admin mặc định nếu chưa có (CHẠY ASYNC)
Task.Run(async () =>
{
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
});

app.Run();
