using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Data;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly OrderDbContext _dbContext;

    public HealthController(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
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
