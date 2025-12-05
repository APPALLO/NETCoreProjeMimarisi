# Catalog Service - Read-Heavy Microservice

Production-ready catalog service with CQRS pattern, aggressive Redis caching, and event-driven architecture.

## Architecture Highlights

### CQRS Pattern
- **Query Service**: Read operations with Redis cache-aside pattern
- **Command Service**: Write operations with cache invalidation
- Separate interfaces for reads and writes

### Caching Strategy
- **Cache-aside pattern**: Check cache → miss → DB → set cache
- **TTL**: 15 minutes for product and category queries
- **Invalidation**: Event-driven on updates
  - Product update → invalidate product cache
  - Product update → invalidate all category pages
- **Key patterns**:
  - `catalog:product:{id}`
  - `catalog:category:{category}:page:{page}:size:{size}`

### Events Published
- **ProductCreatedEvent** → `domain.catalog.ProductCreated`
- **ProductUpdatedEvent** → `domain.catalog.ProductUpdated`

## API Endpoints

### Queries (Read)
```bash
GET /api/products/{id}                    # Get by ID (cached)
GET /api/products/category/{category}     # Get by category (cached, paginated)
GET /api/products/search?q=term           # Search (not cached - dynamic)
```

### Commands (Write)
```bash
POST   /api/products                      # Create product
PUT    /api/products/{id}                 # Update product
PATCH  /api/products/{id}/stock           # Update stock
DELETE /api/products/{id}                 # Soft delete (deactivate)
```

### Health
```bash
GET /health/live                          # Liveness
GET /health/ready                         # Readiness (DB + Redis)
```

## Quick Start

### 1. Setup infrastructure
```powershell
docker-compose up -d
```

### 2. Run migrations
```powershell
cd src/CatalogService.Api
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. Run service
```powershell
dotnet run --urls http://localhost:5001
```

### 4. Test
```powershell
../../scripts/test-catalog.ps1
```

## Example Usage

### Create Product
```bash
curl -X POST http://localhost:5001/api/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Laptop",
    "description": "High-performance laptop",
    "price": 1299.99,
    "category": "Electronics",
    "stockQuantity": 50
  }'
```

### Get Product (1st call: DB, 2nd call: Redis)
```bash
curl http://localhost:5001/api/products/{id}
```

### Get by Category (Paginated)
```bash
curl "http://localhost:5001/api/products/category/Electronics?page=1&pageSize=20"
```

## Performance Characteristics

### Read Operations
- **Cache hit**: ~1-2ms (Redis)
- **Cache miss**: ~50-100ms (PostgreSQL + Redis set)
- **Cache hit ratio target**: >90%

### Write Operations
- **Create**: ~50ms (DB write + RabbitMQ publish)
- **Update**: ~60ms (DB write + cache invalidation + event)

## Cache Invalidation Strategy

When a product is updated:
1. Delete `catalog:product:{id}`
2. Scan and delete all `catalog:category:{category}:*` keys
3. Publish `ProductUpdatedEvent`

**Trade-off**: Category cache invalidation is aggressive (all pages). For high-write scenarios, consider:
- Shorter TTL
- Lazy invalidation
- Cache warming strategies

## Database Schema

```sql
CREATE TABLE products (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description VARCHAR(2000),
    price DECIMAL(18,2) NOT NULL,
    category VARCHAR(100) NOT NULL,
    stock_quantity INT NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP
);

CREATE INDEX idx_products_category ON products(category);
CREATE INDEX idx_products_name ON products(name);
```

## Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=catalogdb;..."
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": "5672"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## Docker Build

```bash
cd src
docker build -f CatalogService.Api/Dockerfile -t catalogservice:latest .
docker run -p 8080:8080 catalogservice:latest
```

## Monitoring

### Key Metrics
- Cache hit/miss ratio
- Query latency (p50, p95, p99)
- Redis connection pool usage
- RabbitMQ publish rate

### Logs
- Cache hits: `LogDebug("Cache hit for product {ProductId}")`
- Cache misses: `LogDebug("Cache miss for product {ProductId}")`
- Commands: `LogInformation("Product created: {ProductId}")`

## Next Steps

- Add integration tests with Testcontainers
- Implement cache warming on startup
- Add Elasticsearch for full-text search
- Implement read replicas for DB
- Add rate limiting per client
