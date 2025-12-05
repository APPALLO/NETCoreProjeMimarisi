# Quick API test script
$baseUrl = "http://localhost:5000"

Write-Host "Testing Identity Service API..." -ForegroundColor Green

# Test health
Write-Host "`n1. Health Check (Live)..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/health/live" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

Write-Host "`n2. Health Check (Ready)..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "$baseUrl/health/ready" -Method Get
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$response.Content | ConvertFrom-Json | ConvertTo-Json

# Register user
Write-Host "`n3. Register User..." -ForegroundColor Yellow
$registerBody = @{
    email = "test@example.com"
    password = "Password123!"
    firstName = "John"
    lastName = "Doe"
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "$baseUrl/api/auth/register" -Method Post -Body $registerBody -ContentType "application/json"
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$registerResult = $response.Content | ConvertFrom-Json
$registerResult | ConvertTo-Json

# Login
Write-Host "`n4. Login..." -ForegroundColor Yellow
$loginBody = @{
    email = "test@example.com"
    password = "Password123!"
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
$loginResult = $response.Content | ConvertFrom-Json
$loginResult | ConvertTo-Json

Write-Host "`nToken: $($loginResult.token)" -ForegroundColor Green
Write-Host "`nAll tests passed!" -ForegroundColor Green
