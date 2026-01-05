using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CustomerService.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<CustomerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient();

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

// Swagger đã tắt theo yêu cầu

var app = builder.Build();

// Configure the HTTP request pipeline.
// Swagger đã tắt theo yêu cầu

// Comment UseHttpsRedirection để tránh lỗi SSL trong development
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "CustomerService" }));

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

            var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var addressFeature = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
            var address = addressFeature?.Addresses.FirstOrDefault(a => a.StartsWith("http://")) ?? "http://localhost:5003";
            
            var uri = new Uri(address);
            var servicePort = uri.Port;
            var serviceName = "CustomerService";
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

app.Run();
