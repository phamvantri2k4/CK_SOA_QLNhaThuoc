using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using Consul;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Health check endpoint for Consul
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "InventoryService" }));

// ===== ĐĂNG KÝ VÀO CONSUL =====
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

// Đăng ký sau khi app đã start để lấy đúng port
lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        try
        {
            // Đợi 1 giây để server hoàn tất khởi động
            await Task.Delay(1000);

            var consulClient = new ConsulClient(config =>
            {
                config.Address = new Uri("http://localhost:8500");
            });

            // Tự động detect port từ server đang chạy
            var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var addressFeature = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
            var address = addressFeature?.Addresses.FirstOrDefault(a => a.StartsWith("http://")) ?? "http://localhost:5006";
            
            var uri = new Uri(address);
            var servicePort = uri.Port;
            var serviceName = "InventoryService";
            var serviceId = $"{serviceName}-{Guid.NewGuid()}";

            Console.WriteLine($"🔍 Detected service running at: {address}");
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
            Console.WriteLine($"✅ {serviceName} đã đăng ký vào Consul tại http://localhost:8500");
            Console.WriteLine($"🏥 Health check: http://localhost:{servicePort}/health");

            // Hủy đăng ký khi service dừng
            lifetime.ApplicationStopping.Register(() =>
            {
                consulClient.Agent.ServiceDeregister(serviceId).Wait();
                Console.WriteLine($"🔴 {serviceName} đã hủy đăng ký khỏi Consul");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi đăng ký Consul: {ex.Message}");
        }
    });
});

app.Run();
