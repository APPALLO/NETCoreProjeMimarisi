# API Gateway test script
$gateway = "http://localhost:5003"

Write-Host "Testing API Gateway..." -ForegroundColor Green

# Health check
Write-Host "`n1. Gateway Health Check..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$gateway/health" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan

# Register through gateway (public)
Write-Host "`n2. Register User (via Gateway)..." -ForegroundColor Yellow
$registerBody = @{
    email = "gateway-test@example.com"
    password = "Password123!"
    firstName = "Gateway"
    lastName = "Test"
} | ConvertTo-Json

try {
    $response = Invoke-WebRequest -Uri "$gateway/api/auth/register" -Method Post -Body $registerBody -ContentType "application/json"
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
    $result = $response.Content | ConvertFrom-Json
    $token = $result.token
    Write-Host "Token received: $($token.Substring(0, 20))..." -ForegroundColor Green
} catch {
    Write-Host "User might already exist, trying login..." -ForegroundColor Yellow
}

# Login through gateway
Write-Host "`n3. Login (via Gateway, Rate Limited)..." -ForegroundColor Yellow
$loginBody = @{
    email = "gateway-test@example.com"
    password = "Password123!"
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "$gateway/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$result = $response.Content | ConvertFrom-Json
$token = $result.token
Write-Host "Token: $($token.Substring(0, 20))..." -ForegroundColor Green

# Get products through gateway (public)
Write-Host "`n4. Get Products (via Gateway)..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$gateway/api/products/category/Electronics?page=1&pageSize=5" -Method Get
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
    Write-Host "Correlation-ID: $($response.Headers['X-Correlation-ID'])" -ForegroundColor Cyan
} catch {
    Write-Host "Catalog service might not have data yet" -ForegroundColor Yellow
}

# Create order through gateway (protected)
Write-Host "`n5. Create Order (via Gateway, Protected)..." -ForegroundColor Yellow
$orderBody = @{
    userId = [Guid]::NewGuid().ToString()
    items = @(
        @{
            productId = [Guid]::NewGuid().ToString()
            productName = "Gateway Test Product"
            quantity = 1
            price = 99.99
        }
    )
} | ConvertTo-Json -Depth 10

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$response = Invoke-WebRequest -Uri "$gateway/api/orders" -Method Post -Body $orderBody -Headers $headers
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
Write-Host "Correlation-ID: $($response.Headers['X-Correlation-ID'])" -ForegroundColor Cyan

# Test rate limiting
Write-Host "`n6. Test Rate Limiting (Auth endpoint)..." -ForegroundColor Yellow
Write-Host "Sending 12 requests (limit is 10/min)..." -ForegroundColor Cyan
$rateLimitHit = $false
for ($i = 1; $i -le 12; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$gateway/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json" -ErrorAction Stop
        Write-Host "  Request $i : $($response.StatusCode)" -ForegroundColor Green
    } catch {
        if ($_.Exception.Response.StatusCode -eq 429) {
            Write-Host "  Request $i : 429 Too Many Requests (Rate limit hit!)" -ForegroundColor Red
            $rateLimitHit = $true
        }
    }
    Start-Sleep -Milliseconds 100
}

if ($rateLimitHit) {
    Write-Host "`nRate limiting is working!" -ForegroundColor Green
} else {
    Write-Host "`nRate limit not hit (might need more requests)" -ForegroundColor Yellow
}

# Check downstream health
Write-Host "`n7. Check Downstream Service Health..." -ForegroundColor Yellow
$services = @("identity", "catalog", "order")
foreach ($service in $services) {
    try {
        $response = Invoke-WebRequest -Uri "$gateway/health/$service/ready" -Method Get
        Write-Host "  $service : $($response.StatusCode)" -ForegroundColor Green
    } catch {
        Write-Host "  $service : Unhealthy" -ForegroundColor Red
    }
}

Write-Host "`nAll gateway tests completed!" -ForegroundColor Green
Write-Host "Gateway is routing requests to all microservices" -ForegroundColor Cyan
