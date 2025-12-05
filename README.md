# Microservices Architecture - .NET 9

Production-ready microservices with event-driven architecture, CQRS, Redis caching, and RabbitMQ messaging.

## Services

| Service | Port | Pattern | Description |
|---------|------|---------|-------------|
| **Identity** | 5000 | Standard | JWT auth, user management |
| **Catalog** | 5001 | CQRS | Product catalog, Redis caching |
| **Order** | 5002 | Saga | Order orchestration, distributed transactions |
| **Gateway** | 5003 | Proxy | YARP reverse proxy, rate limiting |

## Architecture

Each service follows Clean Architecture with 4 layers:
```
src/
├── {Service}.Api/          # Web API, controllers, health checks
├── {Service}.Application/  # Business logic, DTOs, commands/queries
├── {Service}.Domain/       # Entities, value objects, domain events
└── {Service}.Infrastructure/ # EF Core, Redis, RabbitMQ, repositories
```

## Features

- JWT authentication with BCrypt password hashing
- Event-driven architecture (RabbitMQ)
- Redis caching with TTL
- Health checks (/health/live, /health/ready)
- Correlation ID tracking
- Docker multi-stage build
- PostgreSQL with EF Core
- OpenAPI/Swagger documentation

## Quick Start

### 1. Start infrastructure (PostgreSQL, Redis, RabbitMQ)
```powershell
docker-compose up -d
```

### 2. Setup Identity Service
```powershell
.\scripts\setup-local.ps1
dotnet run --project src/IdentityService.Api --urls http://localhost:5000
```

### 3. Setup Catalog Service
```powershell
.\scripts\setup-catalog.ps1
dotnet run --project src/CatalogService.Api --urls http://localhost:5001
```

### 4. Setup Order Service
```powershell
.\scripts\setup-order.ps1
dotnet run --project src/OrderService.Api --urls http://localhost:5002
```

### 5. Setup API Gateway
```powershell
dotnet run --project src/ApiGateway --urls http://localhost:5003
```

### 6. Test services
```powershell
.\scripts\test-api.ps1        # Test Identity Service
.\scripts\test-catalog.ps1    # Test Catalog Service
.\scripts\test-order.ps1      # Test Order Service (Saga)
.\scripts\test-gateway.ps1    # Test API Gateway (All services)
```

## Configuration

Update `appsettings.json` for production:
- Change JWT secret (min 32 chars)
- Update connection strings
- Configure RabbitMQ/Redis endpoints

## Docker Build

```bash
cd src
docker build -f IdentityService.Api/Dockerfile -t identityservice:latest .
docker run -p 8080:8080 identityservice:latest
```

## Event Schema

### Identity Service Events
**UserCreatedEvent** → `domain.identity.UserCreated`
```json
{
  "eventId": "guid",
  "occurredAt": "datetime",
  "version": 1,
  "userId": "guid",
  "email": "string",
  "firstName": "string",
  "lastName": "string"
}
```

### Catalog Service Events
**ProductCreatedEvent** → `domain.catalog.ProductCreated`  
**ProductUpdatedEvent** → `domain.catalog.ProductUpdated`

### Order Service Events (Saga)
**OrderCreatedEvent** → `domain.order.OrderCreated`  
**OrderCompletedEvent** → `domain.order.OrderCompleted`  
**OrderFailedEvent** → `domain.order.OrderFailed`

**Commands to other services:**
- `domain.order.ValidateInventory` → Inventory Service
- `domain.order.ReserveInventory` → Inventory Service
- `domain.order.ProcessPayment` → Billing Service
- `domain.order.ReleaseInventory` → Compensation

## Health Checks

- `GET /health/live` - Liveness probe
- `GET /health/ready` - Readiness probe (checks DB, Redis)

## Infrastructure

- **PostgreSQL**: Separate databases per service (identitydb, catalogdb)
- **Redis**: Shared cache cluster (consider separate instances in production)
- **RabbitMQ**: Topic exchanges for event routing
- **Docker**: Multi-stage builds, non-root users, health checks

## Key Features

✅ Clean Architecture (4 layers per service)  
✅ CQRS pattern (Catalog Service)  
✅ Event-driven communication (RabbitMQ)  
✅ Redis caching with TTL and invalidation  
✅ Health checks (liveness + readiness)  
✅ Correlation ID tracking  
✅ Docker + docker-compose  
✅ Unit tests  
✅ CI/CD pipeline (GitHub Actions)  

## Service Details

- **Identity Service**: See [ARCHITECTURE.md](ARCHITECTURE.md)
- **Catalog Service**: See [CATALOG-README.md](CATALOG-README.md)
- **Order Service**: See [ORDER-README.md](ORDER-README.md)
- **API Gateway**: See [GATEWAY-README.md](GATEWAY-README.md)

## Architecture Patterns

✅ **Clean Architecture**: 4-layer separation per service  
✅ **CQRS**: Read/write separation (Catalog Service)  
✅ **Saga Pattern**: Distributed transaction orchestration (Order Service)  
✅ **API Gateway**: YARP reverse proxy with auth & rate limiting  
✅ **Event-Driven**: RabbitMQ topic exchanges  
✅ **Cache-Aside**: Redis with TTL and invalidation  
✅ **Repository Pattern**: Data access abstraction  
✅ **Dependency Injection**: Interface-based design  
✅ **Rate Limiting**: Per-IP and per-endpoint policies  
✅ **Circuit Breaker**: Health check-based automatic failover  

## API Gateway Routes

All requests go through the gateway at `http://localhost:5003`:

**Public Routes:**
- `POST /api/auth/register` → Identity Service
- `POST /api/auth/login` → Identity Service (rate limited: 10/min)
- `GET /api/products/*` → Catalog Service

**Protected Routes (JWT required):**
- `POST /api/orders` → Order Service
- `GET /api/orders/*` → Order Service

**Health Checks:**
- `GET /health` → Gateway health
- `GET /health/identity/*` → Identity Service
- `GET /health/catalog/*` → Catalog Service
- `GET /health/order/*` → Order Service

## Next Steps

- Inventory Service (event consumer for saga)
- Billing Service (payment processing)
- Shared Contracts NuGet package
- Integration tests with Testcontainers
- Saga timeout & retry handling
- OpenTelemetry + Application Insights
- Azure Key Vault for secrets
- Kubernetes deployment (Helm charts)
- Response caching in gateway
- Request/response transformation
