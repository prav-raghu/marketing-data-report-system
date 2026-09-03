# PowerShell Setup Script for NGINX Proxy

Write-Host "🚀 NGINX Proxy Setup Script" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host ""

$env = Read-Host "Enter environment (development/production)"

if ($env -eq "development") {
    Write-Host "✅ Setting up development environment..." -ForegroundColor Green
    
    Set-Location "$PSScriptRoot\..\..\devops"
    
    if (-not (Test-Path ".env")) {
        Write-Host "📝 Creating .env file..." -ForegroundColor Yellow
        @"
JWT_SECRET=dev_jwt_secret_change_in_production
DATABASE_URL=postgresql://postgres:postgres@postgres:5432/app_database
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=app_database
"@ | Out-File -FilePath ".env" -Encoding UTF8
        Write-Host "✅ .env file created" -ForegroundColor Green
    }
    
    Write-Host "🐳 Starting services with NGINX proxy..." -ForegroundColor Cyan
    docker-compose -f docker-compose.nginx.yml up -d
    
    Write-Host ""
    Write-Host "✅ Development environment ready!" -ForegroundColor Green
    Write-Host "📍 Access points:" -ForegroundColor Yellow
    Write-Host "   - Customer App: http://localhost/"
    Write-Host "   - Admin App: http://localhost/admin/"
    Write-Host "   - API: http://localhost/api/"
    Write-Host ""

} elseif ($env -eq "production") {
    Write-Host "🔐 Setting up production environment..." -ForegroundColor Yellow
    Write-Host ""
    
    $domain = Read-Host "Enter your domain (e.g., yourdomain.com)"
    $email = Read-Host "Enter your email for Let's Encrypt"
    $adminUser = Read-Host "Enter admin username"
    $adminPass = Read-Host "Enter admin password" -AsSecureString
    $adminPassPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPass))
    
    Set-Location "$PSScriptRoot\..\.."
    
    Write-Host "📝 Updating configuration files..." -ForegroundColor Yellow
    
    $files = @(
        "infrastructure\nginx\api-gateway.conf",
        "infrastructure\nginx\customer-web.conf",
        "infrastructure\nginx\admin-web.conf"
    )
    
    foreach ($file in $files) {
        if (Test-Path $file) {
            (Get-Content $file) -replace 'yourdomain.com', $domain | Set-Content $file
        }
    }
    
    Write-Host "🔑 Creating htpasswd file for admin..." -ForegroundColor Yellow
    docker run --rm httpd:alpine htpasswd -Bbn $adminUser $adminPassPlain | Out-File -FilePath "infrastructure\nginx\.htpasswd" -Encoding ASCII -NoNewline
    
    if (-not (Test-Path "devops\.env")) {
        Write-Host "📝 Creating .env file..." -ForegroundColor Yellow
        $jwtSecret = Read-Host "Enter JWT secret" -AsSecureString
        $jwtSecretPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($jwtSecret))
        $dbUrl = Read-Host "Enter database URL"
        
        $randomBytes = New-Object byte[] 32
        [Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($randomBytes)
        $dbPassword = [Convert]::ToBase64String($randomBytes)
        
        @"
JWT_SECRET=$jwtSecretPlain
DATABASE_URL=$dbUrl
POSTGRES_USER=postgres
POSTGRES_PASSWORD=$dbPassword
POSTGRES_DB=app_database
"@ | Out-File -FilePath "devops\.env" -Encoding UTF8
        Write-Host "✅ .env file created" -ForegroundColor Green
    }
    
    Set-Location "devops"
    
    Write-Host "🐳 Starting services..." -ForegroundColor Cyan
    docker-compose -f docker-compose.nginx.yml up -d
    
    Write-Host "📜 Obtaining SSL certificates..." -ForegroundColor Yellow
    docker-compose -f docker-compose.nginx.yml run --rm certbot certonly `
        --webroot `
        --webroot-path=/var/www/certbot `
        --email $email `
        --agree-tos `
        --no-eff-email `
        -d "api.$domain" `
        -d "app.$domain" `
        -d "admin.$domain"
    
    Write-Host "🔄 Reloading NGINX..." -ForegroundColor Cyan
    docker-compose -f docker-compose.nginx.yml exec nginx nginx -s reload
    
    Write-Host ""
    Write-Host "✅ Production environment ready!" -ForegroundColor Green
    Write-Host "📍 Access points:" -ForegroundColor Yellow
    Write-Host "   - Customer App: https://app.$domain"
    Write-Host "   - Admin App: https://admin.$domain (requires authentication)"
    Write-Host "   - API: https://api.$domain"
    Write-Host ""
    Write-Host "⚠️  Important:" -ForegroundColor Red
    Write-Host "   - Update your DNS records to point to this server"
    Write-Host "   - Certificates will auto-renew every 12 hours"
    Write-Host "   - Admin credentials: $adminUser / [hidden]"
    Write-Host ""

} else {
    Write-Host "❌ Invalid environment. Please choose 'development' or 'production'" -ForegroundColor Red
    exit 1
}

Write-Host "🎉 Setup complete!" -ForegroundColor Green
