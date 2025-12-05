using CatalogService.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace CatalogService.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly CatalogDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;

    public HealthController(CatalogDbContext dbContext, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _redis = redis;
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
            checks["redis"] = _redis.IsConnected;
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
