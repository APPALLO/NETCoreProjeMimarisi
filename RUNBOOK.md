# Production Runbook

## 1. Infrastructure Setup

The system uses Docker Compose to orchestrate the following services:
- **PostgreSQL**: Main database (Ports: 5432)
- **Redis**: Caching (Ports: 6379)
- **RabbitMQ**: Message Broker (Ports: 5672, 15672 Management)

### Prerequisites
- Docker Desktop & Docker Compose installed.
- .NET 9.0 SDK (for local build if needed).

## 2. Deployment

### Start Services
```bash
docker-compose up -d --build
```

This command will:
1. Build all microservices (`identity-service`, `catalog-service`, `order-service`, `api-gateway`).
2. Start infrastructure containers (postgres, redis, rabbitmq).
3. Wait for health checks.
4. Start application containers.

### Verify Deployment
Check running containers:
```bash
docker ps
```
All services should be `Up (healthy)` or `Up`.

## 3. Services & Ports

| Service | Internal Port | External Port (Host) | Description |
|---------|---------------|----------------------|-------------|
| ApiGateway | 8080 | 8080 | Entry point for all requests |
| IdentityService | 8080 | - | Auth Service (Internal) |
| CatalogService | 8080 | - | Product Catalog (Internal) |
| OrderService | 8080 | - | Order Management (Internal) |
| RabbitMQ | 5672 | 5672 | Messaging |
| RabbitMQ UI | 15672 | 15672 | Management Interface (user: guest, pass: guest) |
| Postgres | 5432 | 5432 | Database |
| Redis | 6379 | 6379 | Cache |

## 4. Operational Procedures

### Database Migration
Databases are initialized via `scripts/init-databases.sql` on first run.
For schema updates, EF Core migrations are applied on startup (ensure `context.Database.Migrate()` is called in `Program.cs` - *Note: Currently manual or auto-applied in dev*).

### Monitoring
- **Logs**: Use `docker logs <container_name>` (e.g., `docker logs order-service`).
- **RabbitMQ**: Access http://localhost:15672 to monitor queues.
  - Key Queues: `catalog.validate-inventory`, `catalog.reserve-inventory`, `order.inventory-validated`, `order.payment-processed`.

### Troubleshooting
- **Saga Stuck**: Check `RabbitMQ` queues. If messages are in Dead Letter Exchange (DLX) or unacked, check logs for consumer errors.
- **Auth Failures**: Ensure JWT Token is valid. Check `IdentityService` logs. Secret key must match across services (configured in environment variables).

## 5. Disaster Recovery
- **Database Backup**:
  ```bash
  docker exec -t microservices-postgres pg_dumpall -c -U postgres > dump_`date +%d-%m-%Y"_"%H_%M_%S`.sql
  ```
- **Restore**:
  ```bash
  cat dump_file.sql | docker exec -i microservices-postgres psql -U postgres
  ```

## 6. Security Notes
- **Secrets**: Change `Jwt:Secret` in production environment variables.
- **Network**: Services communicate via `microservices-network`. Only `ApiGateway` is exposed to the public (besides infra ports for debugging).
