#!/bin/bash

echo "🚀 NGINX Proxy Setup Script"
echo "============================"
echo ""

read -p "Enter environment (development/production): " ENV

if [ "$ENV" = "development" ]; then
    echo "✅ Setting up development environment..."
    
    cd "$(dirname "$0")/../devops" || exit
    
    if [ ! -f .env ]; then
        echo "📝 Creating .env file..."
        cat > .env << 'EOL'
JWT_SECRET=dev_jwt_secret_change_in_production
DATABASE_URL=postgresql://postgres:postgres@postgres:5432/app_database
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=app_database
EOL
        echo "✅ .env file created"
    fi
    
    echo "🐳 Starting services with NGINX proxy..."
    docker-compose -f docker-compose.nginx.yml up -d
    
    echo ""
    echo "✅ Development environment ready!"
    echo "📍 Access points:"
    echo "   - Customer App: http://localhost/"
    echo "   - Admin App: http://localhost/admin/"
    echo "   - API: http://localhost/api/"
    echo ""

elif [ "$ENV" = "production" ]; then
    echo "🔐 Setting up production environment..."
    echo ""
    
    read -p "Enter your domain (e.g., yourdomain.com): " DOMAIN
    read -p "Enter your email for Let's Encrypt: " EMAIL
    read -p "Enter admin username: " ADMIN_USER
    read -sp "Enter admin password: " ADMIN_PASS
    echo ""
    
    cd "$(dirname "$0")/.." || exit
    
    echo "📝 Updating configuration files..."
    
    sed -i.bak "s/yourdomain.com/$DOMAIN/g" infrastructure/nginx/api-gateway.conf
    sed -i.bak "s/yourdomain.com/$DOMAIN/g" infrastructure/nginx/customer-web.conf
    sed -i.bak "s/yourdomain.com/$DOMAIN/g" infrastructure/nginx/admin-web.conf
    
    echo "🔑 Creating htpasswd file for admin..."
    docker run --rm httpd:alpine htpasswd -Bbn "$ADMIN_USER" "$ADMIN_PASS" > infrastructure/nginx/.htpasswd
    
    if [ ! -f devops/.env ]; then
        echo "📝 Creating .env file..."
        read -sp "Enter JWT secret: " JWT_SECRET
        echo ""
        read -p "Enter database URL: " DB_URL
        
        cat > devops/.env << EOL
JWT_SECRET=$JWT_SECRET
DATABASE_URL=$DB_URL
POSTGRES_USER=postgres
POSTGRES_PASSWORD=$(openssl rand -base64 32)
POSTGRES_DB=app_database
EOL
        echo "✅ .env file created"
    fi
    
    cd devops || exit
    
    echo "🐳 Starting services..."
    docker-compose -f docker-compose.nginx.yml up -d
    
    echo "📜 Obtaining SSL certificates..."
    docker-compose -f docker-compose.nginx.yml run --rm certbot certonly \
        --webroot \
        --webroot-path=/var/www/certbot \
        --email "$EMAIL" \
        --agree-tos \
        --no-eff-email \
        -d "api.$DOMAIN" \
        -d "app.$DOMAIN" \
        -d "admin.$DOMAIN"
    
    echo "🔄 Reloading NGINX..."
    docker-compose -f docker-compose.nginx.yml exec nginx nginx -s reload
    
    echo ""
    echo "✅ Production environment ready!"
    echo "📍 Access points:"
    echo "   - Customer App: https://app.$DOMAIN"
    echo "   - Admin App: https://admin.$DOMAIN (requires authentication)"
    echo "   - API: https://api.$DOMAIN"
    echo ""
    echo "⚠️  Important:"
    echo "   - Update your DNS records to point to this server"
    echo "   - Certificates will auto-renew every 12 hours"
    echo "   - Admin credentials: $ADMIN_USER / [hidden]"
    echo ""

else
    echo "❌ Invalid environment. Please choose 'development' or 'production'"
    exit 1
fi

echo "🎉 Setup complete!"
