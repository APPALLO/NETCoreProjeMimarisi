using CatalogService.Application.DTOs;

namespace CatalogService.Application.Queries;

public record GetProductByIdQuery(Guid ProductId);

public record GetProductsByCategoryQuery(string Category, int Page = 1, int PageSize = 20);

public record SearchProductsQuery(string SearchTerm, int Page = 1, int PageSize = 20);

public interface IProductQueryService
{
    Task<ProductDto?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductDto>> GetByCategoryAsync(string category, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductDto>> SearchAsync(string searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);
}

public record PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
