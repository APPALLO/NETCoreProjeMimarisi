using System.Text.Json;
using CatalogService.Application.DTOs;
using CatalogService.Application.Queries;
using CatalogService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CatalogService.Infrastructure.Services;

public class ProductQueryService : IProductQueryService
{
    private readonly CatalogDbContext _context;
    private readonly IDatabase _redis;
    private readonly ILogger<ProductQueryService> _logger;
    private const int CacheTtlMinutes = 15;

    public ProductQueryService(CatalogDbContext context, IConnectionMultiplexer redis, ILogger<ProductQueryService> logger)
    {
        _context = context;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"catalog:product:{productId}";

        // Cache-aside pattern
        var cached = await _redis.StringGetAsync(cacheKey);
        if (!cached.IsNullOrEmpty)
        {
            _logger.LogDebug("Cache hit for product {ProductId}", productId);
            return JsonSerializer.Deserialize<ProductDto>(cached!);
        }

        _logger.LogDebug("Cache miss for product {ProductId}", productId);
        var product = await _context.Products
            .Where(p => p.Id == productId && p.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (product == null) return null;

        var dto = MapToDto(product);
        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(dto), TimeSpan.FromMinutes(CacheTtlMinutes));

        return dto;
    }

    public async Task<PagedResult<ProductDto>> GetByCategoryAsync(string category, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"catalog:category:{category}:page:{page}:size:{pageSize}";

        var cached = await _redis.StringGetAsync(cacheKey);
        if (!cached.IsNullOrEmpty)
        {
            _logger.LogDebug("Cache hit for category {Category}", category);
            return JsonSerializer.Deserialize<PagedResult<ProductDto>>(cached!)!;
        }

        var query = _context.Products.Where(p => p.Category == category && p.IsActive);
        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<ProductDto>
        {
            Items = products.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(CacheTtlMinutes));

        return result;
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(string searchTerm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .Where(p => p.IsActive && (p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm)));

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>
        {
            Items = products.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static ProductDto MapToDto(Domain.Entities.Product product) => new()
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
