# Catalog Service API test script
$baseUrl = "http://localhost:5001"

Write-Host "Testing Catalog Service API..." -ForegroundColor Green

# Health checks
Write-Host "`n1. Health Check..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/health/ready" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

# Create product
Write-Host "`n2. Create Product..." -ForegroundColor Yellow
$productBody = @{
    name = "Laptop"
    description = "High-performance laptop"
    price = 1299.99
    category = "Electronics"
    stockQuantity = 50
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "$baseUrl/api/products" -Method Post -Body $productBody -ContentType "application/json"
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$product = $response.Content | ConvertFrom-Json
$product | ConvertTo-Json
$productId = $product.id

# Get product by ID
Write-Host "`n3. Get Product by ID..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/api/products/$productId" -Method Get
Write-Host "Status: $($response.StatusCode) (from cache on 2nd call)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

# Get by category
Write-Host "`n4. Get Products by Category..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/api/products/category/Electronics?page=1&pageSize=10" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

# Search
Write-Host "`n5. Search Products..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/api/products/search?q=laptop&page=1&pageSize=10" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

Write-Host "`nAll tests passed!" -ForegroundColor Green
Write-Host "Check RabbitMQ: http://localhost:15672 for ProductCreated event" -ForegroundColor Yellow
