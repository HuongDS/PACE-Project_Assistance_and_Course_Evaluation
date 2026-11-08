using MockProject.Business.Interfaces;
using MockProject.Business.Services;
using MockProject.Data.Interfaces;
using MockProject.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Restaurant Order & Payment API (Mock Project)",
        Version = "v1",
        Description = "Mock project kiến trúc 3-layer mô phỏng quy trình: Bắt đầu -> Khách chọn món -> Kiểm tra bếp -> Bếp xác nhận -> Khách thanh toán -> Hoàn tất đơn."
    });
});

// Đăng ký tầng Data (Repositories - In Memory Singleton để lưu giữ trạng thái)
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IKitchenRepository, KitchenRepository>();
builder.Services.AddSingleton<IPaymentRepository, PaymentRepository>();

// Đăng ký tầng Business (Services)
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IKitchenService, KitchenService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Cấu hình CORS để tiện test với UI nếu cần
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Restaurant Order & Payment API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
