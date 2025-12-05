# Catalog Service setup script
Write-Host "Setting up Catalog Service..." -ForegroundColor Green

# Infrastructure should already be running from Identity setup
Write-Host "`nChecking Docker containers..." -ForegroundColor Yellow
docker ps | Select-String "postgres|redis|rabbitmq"

# Check if dotnet-ef is installed
$efInstalled = dotnet tool list -g | Select-String "dotnet-ef"
if (-not $efInstalled) {
    Write-Host "`nInstalling EF Core tools..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

# Run migrations
Write-Host "`nRunning database migrations..." -ForegroundColor Yellow
Set-Location src/CatalogService.Api
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
dotnet ef database update
Set-Location ../..

Write-Host "`nCatalog Service ready! Run with:" -ForegroundColor Green
Write-Host "  dotnet run --project src/CatalogService.Api --urls http://localhost:5001" -ForegroundColor Cyan
Write-Host "`nEndpoints:" -ForegroundColor Green
Write-Host "  GET  /api/products/{id}" -ForegroundColor Cyan
Write-Host "  GET  /api/products/category/{category}" -ForegroundColor Cyan
Write-Host "  GET  /api/products/search?q=term" -ForegroundColor Cyan
Write-Host "  POST /api/products" -ForegroundColor Cyan
