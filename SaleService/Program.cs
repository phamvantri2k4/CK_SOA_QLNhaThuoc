using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SaleService.Data;
using Shared.Services;
using Consul;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddMemoryCache(); // Đăng ký Memory Cache

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
builder.Services.AddHttpClient();

// Consul Service Discovery
builder.Services.AddSingleton<ConsulServiceDiscovery>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "SaleService" }));

// ===== ĐĂNG KÝ VÀO CONSUL =====
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        try
        {
            await Task.Delay(1000);

            var consulClient = new ConsulClient(config =>
            {
                config.Address = new Uri("http://localhost:8500");
            });

            var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var addressFeature = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
            var address = addressFeature?.Addresses.FirstOrDefault(a => a.StartsWith("http://")) ?? "http://localhost:5002";
            
            var uri = new Uri(address);
            var servicePort = uri.Port;
            var serviceName = "SaleService";
            var serviceId = $"{serviceName}-{Guid.NewGuid()}";

            Console.WriteLine($"🔍 Detected {serviceName} at: {address}");
            Console.WriteLine($"📝 Registering to Consul with port: {servicePort}");

            var registration = new AgentServiceRegistration
            {
                ID = serviceId,
                Name = serviceName,
                Address = "localhost",
                Port = servicePort,
                Check = new AgentServiceCheck
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
                Console.WriteLine($"🔴 {serviceName} đã hủy đăng ký khỏi Consul");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi Consul: {ex.Message}");
        }
    });
});

app.Run();
