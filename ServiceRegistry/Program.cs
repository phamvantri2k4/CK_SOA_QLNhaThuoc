using ServiceRegistry.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// In-memory service registry (có thể thay bằng database sau)
// Lưu trữ dịch vụ ngay trong bộ nhớ cho đơn giản.
var ttlSeconds = builder.Configuration.GetValue<int?>("ServiceRegistry:TtlSeconds") ?? 60;
var store = new ServiceRegistryStore(TimeSpan.FromSeconds(ttlSeconds));
builder.Services.AddSingleton(store);

var app = builder.Build();

// Tắt Swagger để đơn giản hóa
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

// Comment UseHttpsRedirection để tránh lỗi SSL trong development
// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Background cleanup: chủ động purge service hết hạn (không phụ thuộc request)
var cleanupIntervalSeconds = Math.Max(5, ttlSeconds / 2);
_ = Task.Run(async () =>
{
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(cleanupIntervalSeconds), app.Lifetime.ApplicationStopping);
            store.PurgeExpired();
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

app.Run();

