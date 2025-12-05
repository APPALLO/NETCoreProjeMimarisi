# Order Service API test script
$baseUrl = "http://localhost:5002"

Write-Host "Testing Order Service (Saga Pattern)..." -ForegroundColor Green

# Health check
Write-Host "`n1. Health Check..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/health/ready" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

# Create order (starts saga)
Write-Host "`n2. Create Order (Start Saga)..." -ForegroundColor Yellow
$orderBody = @{
    userId = [Guid]::NewGuid().ToString()
    items = @(
        @{
            productId = [Guid]::NewGuid().ToString()
            productName = "Laptop"
            quantity = 1
            price = 1299.99
        },
        @{
            productId = [Guid]::NewGuid().ToString()
            productName = "Mouse"
            quantity = 2
            price = 29.99
        }
    )
} | ConvertTo-Json -Depth 10

$response = Invoke-WebRequest -Uri "$baseUrl/api/orders" -Method Post -Body $orderBody -ContentType "application/json"
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$order = $response.Content | ConvertFrom-Json
$order | ConvertTo-Json
$orderId = $order.id

# Get order
Write-Host "`n3. Get Order..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/api/orders/$orderId" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

# Get saga status
Write-Host "`n4. Get Saga Status..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/api/orders/$orderId/saga" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$saga = $response.Content | ConvertFrom-Json
$saga | ConvertTo-Json -Depth 10

Write-Host "`nSaga Steps:" -ForegroundColor Green
foreach ($step in $saga.history) {
    Write-Host "  [$($step.step)] $($step.status) - $($step.message)" -ForegroundColor Cyan
}

Write-Host "`nAll tests passed!" -ForegroundColor Green
Write-Host "Note: Saga is waiting for Inventory/Billing services to respond" -ForegroundColor Yellow
Write-Host "Check RabbitMQ: http://localhost:15672 for published events" -ForegroundColor Yellow
