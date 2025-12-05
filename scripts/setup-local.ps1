# Local development setup script
Write-Host "Starting Identity Service local setup..." -ForegroundColor Green

# Start infrastructure
Write-Host "`nStarting Docker containers..." -ForegroundColor Yellow
docker-compose up -d

# Wait for services
Write-Host "`nWaiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Check if dotnet-ef is installed
$efInstalled = dotnet tool list -g | Select-String "dotnet-ef"
if (-not $efInstalled) {
    Write-Host "`nInstalling EF Core tools..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

# Run migrations
Write-Host "`nRunning database migrations..." -ForegroundColor Yellow
Set-Location src/IdentityService.Api
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
dotnet ef database update
Set-Location ../..

Write-Host "`nSetup complete! Run the service with:" -ForegroundColor Green
Write-Host "  dotnet run --project src/IdentityService.Api" -ForegroundColor Cyan
Write-Host "`nOr build Docker image:" -ForegroundColor Green
Write-Host "  cd src && docker build -f IdentityService.Api/Dockerfile -t identityservice:latest ." -ForegroundColor Cyan
Write-Host "`nHealth checks:" -ForegroundColor Green
Write-Host "  http://localhost:5000/health/live" -ForegroundColor Cyan
Write-Host "  http://localhost:5000/health/ready" -ForegroundColor Cyan
Write-Host "`nRabbitMQ Management: http://localhost:15672 (guest/guest)" -ForegroundColor Green
