using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public HealthController(IdentityDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var checks = new Dictionary<string, bool>();

        try
        {
            await _dbContext.Database.CanConnectAsync();
            checks["database"] = true;
        }
        catch
        {
            checks["database"] = false;
        }

        try
        {
            var redis = ConnectionMultiplexer.Connect(_configuration["Redis:ConnectionString"] ?? "localhost:6379");
            checks["redis"] = redis.IsConnected;
        }
        catch
        {
            checks["redis"] = false;
        }

        var allHealthy = checks.All(c => c.Value);
        var statusCode = allHealthy ? 200 : 503;

        return StatusCode(statusCode, new
        {
            status = allHealthy ? "ready" : "not_ready",
            checks,
            timestamp = DateTime.UtcNow
        });
    }
}
