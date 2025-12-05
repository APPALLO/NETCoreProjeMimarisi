using CatalogService.Application.Commands;
using CatalogService.Application.DTOs;
using CatalogService.Domain.Entities;
using CatalogService.Domain.Events;
using CatalogService.Infrastructure.Data;
using CatalogService.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CatalogService.Infrastructure.Services;

public class ProductCommandService : IProductCommandService
{
    private readonly CatalogDbContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDatabase _redis;
    private readonly ILogger<ProductCommandService> _logger;

    public ProductCommandService(
        CatalogDbContext context,
        IEventPublisher eventPublisher,
        IConnectionMultiplexer redis,
        ILogger<ProductCommandService> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<ProductDto> CreateAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = Product.Create(
            command.Name,
            command.Description,
            command.Price,
            command.Category,
            command.StockQuantity);

        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var @event = new ProductCreatedEvent
        {
            ProductId = product.Id,
            Name = product.Name,
            Price = product.Price,
            Category = product.Category
        };

        await _eventPublisher.PublishAsync("domain.catalog.ProductCreated", @event, cancellationToken);

        _logger.LogInformation("Product created: {ProductId}", product.Id);

        return MapToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { command.ProductId }, cancellationToken);
        if (product == null)
            throw new InvalidOperationException($"Product {command.ProductId} not found");

        product.UpdateDetails(command.Name, command.Description, command.Price, command.Category);
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await InvalidateCacheAsync(product.Id, product.Category);

        var @event = new ProductUpdatedEvent
        {
            ProductId = product.Id,
            Name = product.Name,
            Price = product.Price
        };

        await _eventPublisher.PublishAsync("domain.catalog.ProductUpdated", @event, cancellationToken);

        _logger.LogInformation("Product updated: {ProductId}", product.Id);

        return MapToDto(product);
    }

    public async Task UpdateStockAsync(UpdateStockCommand command, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { command.ProductId }, cancellationToken);
        if (product == null)
            throw new InvalidOperationException($"Product {command.ProductId} not found");

        product.UpdateStock(command.Quantity);
        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateCacheAsync(product.Id, product.Category);

        _logger.LogInformation("Stock updated for product {ProductId}: {Quantity}", product.Id, command.Quantity);
    }

    public async Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product == null)
            throw new InvalidOperationException($"Product {productId} not found");

        product.Deactivate();
        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateCacheAsync(product.Id, product.Category);

        _logger.LogInformation("Product deactivated: {ProductId}", productId);
    }

    private async Task InvalidateCacheAsync(Guid productId, string category)
    {
        await _redis.KeyDeleteAsync($"catalog:product:{productId}");
        
        // Invalidate category cache (all pages)
        var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: $"catalog:category:{category}:*");
        foreach (var key in keys)
        {
            await _redis.KeyDeleteAsync(key);
        }
    }

    private static ProductDto MapToDto(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        Category = product.Category,
        StockQuantity = product.StockQuantity,
        IsActive = product.IsActive,
        CreatedAt = product.CreatedAt
    };
}
