using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using ReportingService.Data;
using ReportingService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<ReportingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Tắt Swagger để đúng kiến trúc SOA và tránh tự mở swagger trên trình duyệt

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ReportingService" }));

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
            var address = addressFeature?.Addresses.FirstOrDefault(a => a.StartsWith("http://")) ?? "http://localhost:5277";
            
            var uri = new Uri(address);
            var servicePort = uri.Port;
            var serviceName = "ReportingService";
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
