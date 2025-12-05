using CatalogService.Application.DTOs;

namespace CatalogService.Application.Commands;

public record CreateProductCommand(string Name, string Description, decimal Price, string Category, int StockQuantity);

public record UpdateProductCommand(Guid ProductId, string Name, string Description, decimal Price, string Category);

public record UpdateStockCommand(Guid ProductId, int Quantity);

public interface IProductCommandService
{
    Task<ProductDto> CreateAsync(CreateProductCommand command, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
    Task UpdateStockAsync(UpdateStockCommand command, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default);
}
