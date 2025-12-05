namespace CatalogService.Application.Saga;

public record ValidateInventoryCommand
{
    public Guid OrderId { get; init; }
    public List<InventoryItem> Items { get; init; } = new();
}

public record ReserveInventoryCommand
{
    public Guid OrderId { get; init; }
    public List<InventoryItem> Items { get; init; } = new();
}

public record ReleaseInventoryCommand
{
    public Guid OrderId { get; init; }
}

public record InventoryItem
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
}

public record InventoryValidatedEvent
{
    public Guid OrderId { get; init; }
    public bool IsValid { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record InventoryReservedEvent
{
    public Guid OrderId { get; init; }
    public bool Success { get; init; }
    public string Reason { get; init; } = string.Empty;
}
