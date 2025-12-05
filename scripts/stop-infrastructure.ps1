# Stop all infrastructure services
Write-Host "Stopping Infrastructure Services..." -ForegroundColor Yellow

docker compose down

Write-Host "✓ All services stopped" -ForegroundColor Green
Write-Host ""
Write-Host "To remove volumes (delete all data):" -ForegroundColor Yellow
Write-Host "  docker compose down -v" -ForegroundColor Cyan
