# Start all infrastructure services
Write-Host "Starting Infrastructure Services..." -ForegroundColor Green

# Check if Docker is running
try {
    docker ps | Out-Null
    Write-Host "✓ Docker is running" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker is not running!" -ForegroundColor Red
    Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
    exit 1
}

# Start services
Write-Host "`nStarting PostgreSQL, Redis, and RabbitMQ..." -ForegroundColor Yellow
docker compose up -d

# Wait for services to be ready
Write-Host "`nWaiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Check PostgreSQL
Write-Host "`nChecking PostgreSQL..." -ForegroundColor Yellow
$pgReady = docker exec microservices-postgres pg_isready -U postgres
if ($pgReady -like "*accepting connections*") {
    Write-Host "✓ PostgreSQL is ready" -ForegroundColor Green
} else {
    Write-Host "✗ PostgreSQL is not ready" -ForegroundColor Red
}

# Check Redis
Write-Host "`nChecking Redis..." -ForegroundColor Yellow
$redisReady = docker exec identity-redis redis-cli ping
if ($redisReady -eq "PONG") {
    Write-Host "✓ Redis is ready" -ForegroundColor Green
} else {
    Write-Host "✗ Redis is not ready" -ForegroundColor Red
}

# Check RabbitMQ
Write-Host "`nChecking RabbitMQ..." -ForegroundColor Yellow
$rabbitReady = docker exec identity-rabbitmq rabbitmq-diagnostics ping
if ($rabbitReady -like "*Ping succeeded*") {
    Write-Host "✓ RabbitMQ is ready" -ForegroundColor Green
} else {
    Write-Host "✗ RabbitMQ is not ready" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Infrastructure Services Status:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PostgreSQL: localhost:5432" -ForegroundColor White
Write-Host "  - Database: identitydb, catalogdb, orderdb" -ForegroundColor Gray
Write-Host "  - User: postgres / Password: postgres" -ForegroundColor Gray
Write-Host ""
Write-Host "Redis: localhost:6379" -ForegroundColor White
Write-Host "  - No password required" -ForegroundColor Gray
Write-Host ""
Write-Host "RabbitMQ: localhost:5672" -ForegroundColor White
Write-Host "  - Management UI: http://localhost:15672" -ForegroundColor Gray
Write-Host "  - User: guest / Password: guest" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nInfrastructure is ready! You can now start the services." -ForegroundColor Green
