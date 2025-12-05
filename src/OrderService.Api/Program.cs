using Microsoft.EntityFrameworkCore;
using OrderService.Application.Services;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<OrderService.Application.Services.IOrderRepository, OrderService.Infrastructure.Repositories.OrderRepository>();
builder.Services.AddScoped<OrderService.Application.Services.ISagaRepository, OrderService.Infrastructure.Repositories.SagaRepository>();
builder.Services.AddScoped<IOrderSagaOrchestrator, OrderSagaOrchestrator>();
builder.Services.AddScoped<IOrderQueryService, OrderQueryService>();
builder.Services.AddSingleton<OrderService.Application.Services.IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddHostedService<OrderSagaConsumer>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("X-Correlation-ID"))
    {
        context.Request.Headers.Append("X-Correlation-ID", Guid.NewGuid().ToString());
    }
    context.Response.Headers.Append("X-Correlation-ID", context.Request.Headers["X-Correlation-ID"].ToString());
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
