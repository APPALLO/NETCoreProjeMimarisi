using Microsoft.EntityFrameworkCore;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public OrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders.FindAsync(new object[] { orderId }, cancellationToken);
    }

    public async Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class SagaRepository : ISagaRepository
{
    private readonly OrderDbContext _context;

    public SagaRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<OrderSaga?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.OrderSagas.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
    }

    public async Task AddAsync(OrderSaga saga, CancellationToken cancellationToken = default)
    {
        await _context.OrderSagas.AddAsync(saga, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrderSaga saga, CancellationToken cancellationToken = default)
    {
        _context.OrderSagas.Update(saga);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
