# Order Service setup script
Write-Host "Setting up Order Service..." -ForegroundColor Green

Write-Host "`nChecking Docker containers..." -ForegroundColor Yellow
docker ps | Select-String "postgres|rabbitmq"

$efInstalled = dotnet tool list -g | Select-String "dotnet-ef"
if (-not $efInstalled) {
    Write-Host "`nInstalling EF Core tools..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

Write-Host "`nRunning database migrations..." -ForegroundColor Yellow
Set-Location src/OrderService.Api
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
dotnet ef database update
Set-Location ../..

Write-Host "`nOrder Service ready! Run with:" -ForegroundColor Green
Write-Host "  dotnet run --project src/OrderService.Api --urls http://localhost:5002" -ForegroundColor Cyan
Write-Host "`nEndpoints:" -ForegroundColor Green
Write-Host "  POST /api/orders              # Create order (starts saga)" -ForegroundColor Cyan
Write-Host "  GET  /api/orders/{id}         # Get order" -ForegroundColor Cyan
Write-Host "  GET  /api/orders/{id}/saga    # Get saga status" -ForegroundColor Cyan
Write-Host "  GET  /api/orders/user/{userId} # Get user orders" -ForegroundColor Cyan
