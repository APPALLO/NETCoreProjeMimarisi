# Identity Service - Architecture Documentation

## Overview
Production-ready microservice implementing authentication and user management with event-driven architecture.

## Technology Stack
- .NET 9.0
- PostgreSQL (EF Core)
- Redis (caching)
- RabbitMQ (event bus)
- JWT authentication
- Docker

## Project Structure

```
src/
├── IdentityService.Api/              # Web API Layer
│   ├── Controllers/                  # API endpoints
│   ├── Program.cs                    # DI & middleware setup
│   └── Dockerfile                    # Multi-stage build
│
├── IdentityService.Application/      # Business Logic
│   ├── Services/                     # Auth service, interfaces
│   └── DTOs/                         # Request/response models
│
├── IdentityService.Domain/           # Domain Layer
│   ├── Entities/                     # User entity
│   └── Events/                       # Domain events
│
├── IdentityService.Infrastructure/   # Infrastructure
│   ├── Data/                         # EF Core DbContext
│   ├── Repositories/                 # Data access
│   ├── Messaging/                    # RabbitMQ publisher
│   └── Caching/                      # Redis implementation
│
└── IdentityService.Tests/            # Unit & Integration tests
```

## Key Design Decisions

### 1. Clean Architecture
- Domain layer has no dependencies
- Application layer depends only on Domain
- Infrastructure implements Application interfaces
- API layer orchestrates everything

### 2. Event-Driven Communication
- **Exchange**: `domain.identity.UserCreated`
- **Pattern**: Topic exchange for flexibility
- **Idempotency**: Every event has `eventId`, `occurredAt`, `version`
- **Future**: Implement Transactional Outbox pattern for guaranteed delivery

### 3. Caching Strategy
- **Pattern**: Cache-aside
- **Key format**: `identity:user:{id}`
- **TTL**: 15 minutes for user data
- **Invalidation**: Event-driven (future enhancement)

### 4. Security
- BCrypt password hashing (cost factor 11)
- JWT with HS256 signing
- Secrets via appsettings (production: Azure Key Vault)
- Non-root Docker user
- HTTPS enforcement

### 5. Observability
- Correlation ID tracking (X-Correlation-ID header)
- Health checks: `/health/live` (liveness), `/health/ready` (readiness)
- Structured logging ready (JSON format)
- Future: OpenTelemetry + Application Insights

## API Endpoints

### Authentication
- `POST /api/auth/register` - Create new user
- `POST /api/auth/login` - Authenticate user
- `POST /api/auth/validate` - Validate JWT token

### Health
- `GET /health/live` - Liveness probe
- `GET /health/ready` - Readiness probe (checks DB, Redis)

## Events Published

### UserCreatedEvent
```json
{
  "eventId": "guid",
  "occurredAt": "2024-12-05T10:30:00Z",
  "version": 1,
  "userId": "guid",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Exchange**: `domain.identity.UserCreated`
**Routing Key**: (empty for topic exchange)

## Configuration

### Required Settings
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=identitydb;..."
  },
  "Jwt": {
    "Secret": "min-32-chars-secret",
    "Issuer": "IdentityService",
    "Audience": "MicroserviceClients"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": "5672",
    "Username": "guest",
    "Password": "guest"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## Deployment

### Docker Build
```bash
cd src
docker build -f IdentityService.Api/Dockerfile -t identityservice:latest .
```

### Kubernetes (AKS)
- Liveness probe: `/health/live`
- Readiness probe: `/health/ready`
- Resource limits: 500m CPU, 512Mi memory (adjust based on load)
- Horizontal Pod Autoscaler: target 70% CPU

### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Development/Staging/Production
- `ConnectionStrings__DefaultConnection`: DB connection
- `Jwt__Secret`: From Azure Key Vault
- `RabbitMQ__Host`, `Redis__ConnectionString`: Service endpoints

## Testing Strategy

### Unit Tests
- Service layer logic
- Domain entity behavior
- Mocked dependencies (Moq)

### Integration Tests (Future)
- Real DB (Testcontainers)
- Real Redis/RabbitMQ
- End-to-end flows

### Contract Tests (Future)
- Event schema validation
- Consumer-driven contracts

## Performance Considerations

### Current
- Redis caching reduces DB load
- Connection pooling (EF Core default)
- Async/await throughout

### Future Optimizations
- Read replicas for DB
- Redis cluster for HA
- Rate limiting per user
- Token blacklist in Redis

## Security Checklist

- [x] Password hashing (BCrypt)
- [x] JWT signing
- [x] HTTPS enforcement
- [x] Non-root Docker user
- [ ] Azure Key Vault integration
- [ ] Rate limiting
- [ ] Input validation (FluentValidation)
- [ ] CORS configuration
- [ ] SQL injection protection (EF Core parameterized)

## Monitoring & Alerts

### Metrics to Track
- Request latency (p50, p95, p99)
- Error rate
- Token generation rate
- Cache hit/miss ratio
- RabbitMQ queue depth

### Alerts
- Error rate > 5%
- Latency p95 > 500ms
- DB connection failures
- Redis unavailable

## Next Steps

1. **Refresh Tokens**: Implement refresh token flow
2. **Email Verification**: Send verification emails
3. **Password Reset**: Forgot password flow
4. **2FA**: TOTP-based two-factor auth
5. **Outbox Pattern**: Guaranteed event delivery
6. **Integration Tests**: Testcontainers setup
7. **Observability**: OpenTelemetry + App Insights
8. **Key Vault**: Azure Key Vault for secrets
