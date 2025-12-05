using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Commands;
using OrderService.Application.DTOs;
using OrderService.Application.Services;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderSagaOrchestrator _orchestrator;
    private readonly IOrderQueryService _queryService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderSagaOrchestrator orchestrator,
        IOrderQueryService queryService,
        ILogger<OrdersController> logger)
    {
        _orchestrator = orchestrator;
        _queryService = queryService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var orderId = await _orchestrator.StartOrderSagaAsync(command, cancellationToken);
            var order = await _queryService.GetByIdAsync(orderId, cancellationToken);
            
            return AcceptedAtAction(nameof(GetById), new { id = orderId }, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _queryService.GetByIdAsync(id, cancellationToken);
        return order == null ? NotFound() : Ok(order);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<List<OrderDto>>> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        var orders = await _queryService.GetByUserIdAsync(userId, cancellationToken);
        return Ok(orders);
    }

    [HttpGet("{id:guid}/saga")]
    public async Task<ActionResult<OrderSagaDto>> GetSagaStatus(Guid id, CancellationToken cancellationToken)
    {
        var saga = await _queryService.GetSagaByOrderIdAsync(id, cancellationToken);
        return saga == null ? NotFound() : Ok(saga);
    }
}
