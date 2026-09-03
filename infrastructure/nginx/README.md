# NGINX Proxy Configuration Guide

This directory contains NGINX configuration files for proxying requests to the monorepo services in both development and production environments.

## Configuration Files

### Main Configuration Files

- **`nginx.conf`** - Main NGINX configuration with:
  - Performance optimizations (worker processes, connections, keepalive)
  - Gzip and Brotli compression
  - Rate limiting zones (API, auth, general traffic)
  - SSL/TLS settings
  - Upstream definitions for all services
  - WebSocket support via connection upgrade mapping
  - HTTP to HTTPS redirect (port 80 → 443)

### Service-Specific Configurations

- **`api-gateway.conf`** - Production API Gateway proxy configuration
  - Routes: `/api/v1/auth/`, `/api/v1/customer/`, `/api/v1/admin/`, `/api/v1/schedule/`
  - WebSocket support for `/socket.io/`
  - SSL/TLS with HTTP/2
  - Security headers (HSTS, X-Frame-Options, CSP, etc.)
  - Rate limiting (100 req/s general, 10 req/s for auth)
  - Domain: `api.yourdomain.com`

- **`customer-web.conf`** - Customer web app (production)
  - Static file serving with caching
  - SPA routing (try_files with fallback to index.html)
  - API proxy to gateway
  - WebSocket support
  - Domain: `app.yourdomain.com`

- **`admin-web.conf`** - Admin web app (production)
  - Static file serving with caching
  - HTTP Basic Auth protection
  - Enhanced security headers (stricter CSP, X-Frame-Options: DENY)
  - API proxy to gateway
  - Domain: `admin.yourdomain.com`

- **`development.conf`** - Local development configuration
  - Simple HTTP (no SSL)
  - Routes: `/api/`, `/customer/`, `/admin/`, `/`
  - WebSocket support
  - Localhost only
  - No authentication

### Docker Configuration

- **`Dockerfile`** - NGINX Docker image based on Alpine Linux
  - Installs OpenSSL for SSL certificate management
  - Creates cache and certificate directories
  - Copies base configuration files

- **`docker-compose.nginx.yml`** - Full stack with NGINX reverse proxy
  - NGINX proxy container
  - All backend services (api-gateway, customer-api, admin-api, schedule-api)
  - Frontend services (customer-web, admin-web)
  - PostgreSQL database
  - Redis cache
  - Certbot for SSL certificate management
  - Shared network for inter-container communication

## Setup Options

### Option 1: Development with Docker Compose + NGINX

```bash
# From devops directory
docker-compose -f docker-compose.nginx.yml up -d

# Access services through NGINX
# Frontend: http://localhost/
# Customer App: http://localhost/customer/
# Admin App: http://localhost/admin/
# API: http://localhost/api/
```

### Option 2: Production Deployment

#### Step 1: Update Domain Names

Edit the configuration files and replace `yourdomain.com` with your actual domain:

- `api-gateway.conf`: `api.yourdomain.com`
- `customer-web.conf`: `app.yourdomain.com`
- `admin-web.conf`: `admin.yourdomain.com`

#### Step 2: Generate SSL Certificates

**Option A: Let's Encrypt with Certbot (Recommended)**

```bash
# Initial certificate generation
docker-compose -f docker-compose.nginx.yml run --rm certbot certonly \
  --webroot \
  --webroot-path=/var/www/certbot \
  --email your-email@example.com \
  --agree-tos \
  --no-eff-email \
  -d api.yourdomain.com \
  -d app.yourdomain.com \
  -d admin.yourdomain.com

# Auto-renewal is configured in docker-compose.nginx.yml
```

**Option B: Self-Signed Certificates (Development/Testing)**

```bash
# Generate self-signed certificates
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout infrastructure/nginx/ssl/privkey.pem \
  -out infrastructure/nginx/ssl/fullchain.pem \
  -subj "/CN=localhost"
```

#### Step 3: Configure Admin Basic Auth

```bash
# Create htpasswd file for admin panel
docker run --rm httpd:alpine htpasswd -Bbn admin your_secure_password > infrastructure/nginx/.htpasswd
```

#### Step 4: Update docker-compose.nginx.yml

Mount production configs instead of development.conf:

```yaml
nginx:
  volumes:
    - ../../infrastructure/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
    - ../../infrastructure/nginx/api-gateway.conf:/etc/nginx/conf.d/api-gateway.conf:ro
    - ../../infrastructure/nginx/customer-web.conf:/etc/nginx/conf.d/customer-web.conf:ro
    - ../../infrastructure/nginx/admin-web.conf:/etc/nginx/conf.d/admin-web.conf:ro
```

#### Step 5: Deploy

```bash
# Set environment variables
export JWT_SECRET=your_jwt_secret
export DATABASE_URL=postgresql://user:password@postgres:5432/dbname

# Start all services
docker-compose -f docker-compose.nginx.yml up -d

# Check logs
docker-compose -f docker-compose.nginx.yml logs -f nginx
```

### Option 3: Standalone NGINX (Existing Infrastructure)

If you already have NGINX installed on your server:

#### Step 1: Install NGINX (if not installed)

**Ubuntu/Debian:**
```bash
sudo apt update
sudo apt install nginx
```

**CentOS/RHEL:**
```bash
sudo yum install nginx
```

#### Step 2: Copy Configuration Files

```bash
# Copy main config
sudo cp infrastructure/nginx/nginx.conf /etc/nginx/nginx.conf

# Copy site configs
sudo cp infrastructure/nginx/api-gateway.conf /etc/nginx/conf.d/
sudo cp infrastructure/nginx/customer-web.conf /etc/nginx/conf.d/
sudo cp infrastructure/nginx/admin-web.conf /etc/nginx/conf.d/
```

#### Step 3: Update Upstream Servers

Edit the configuration files to point to your actual service locations:

```nginx
upstream api_gateway {
    server localhost:3000;  # or actual IP/hostname
}
```

#### Step 4: Test and Reload

```bash
# Test configuration
sudo nginx -t

# Reload NGINX
sudo systemctl reload nginx
```

## Configuration Details

### Rate Limiting

Three rate limit zones are configured:

- **`api_limit`**: 100 requests/second for general API traffic
- **`auth_limit`**: 10 requests/second for authentication endpoints
- **`general_limit`**: 200 requests/second for frontend traffic

Adjust in `nginx.conf`:

```nginx
limit_req_zone $binary_remote_addr zone=api_limit:10m rate=100r/s;
```

### Caching

API response caching is configured but disabled by default. To enable caching for specific routes:

```nginx
location /api/v1/public/ {
    proxy_cache api_cache;
    proxy_cache_valid 200 5m;
    proxy_cache_key "$scheme$request_method$host$request_uri";
    add_header X-Cache-Status $upstream_cache_status;
    
    proxy_pass http://api_gateway;
}
```

### WebSocket Support

WebSocket connections are supported via the connection upgrade mapping:

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

location /socket.io/ {
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection $connection_upgrade;
    # ... other settings
}
```

### Security Headers

Production configurations include comprehensive security headers:

- **HSTS**: Forces HTTPS for 1 year
- **X-Frame-Options**: Prevents clickjacking
- **X-Content-Type-Options**: Prevents MIME sniffing
- **CSP**: Content Security Policy (admin panel)
- **Referrer-Policy**: Controls referrer information

### Static File Caching

Static assets are cached aggressively:

- HTML files: 1 hour
- CSS/JS/Images/Fonts: 1 year with `immutable` flag

```nginx
location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
    expires 1y;
    add_header Cache-Control "public, immutable";
    access_log off;
}
```

## Monitoring & Logging

### Log Locations

- **Access logs**: `/var/log/nginx/access.log`
- **Error logs**: `/var/log/nginx/error.log`

### Custom Log Format

The access log includes upstream response times for performance monitoring:

```nginx
log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                '$status $body_bytes_sent "$http_referer" '
                '"$http_user_agent" "$http_x_forwarded_for" '
                'rt=$request_time uct="$upstream_connect_time" '
                'uht="$upstream_header_time" urt="$upstream_response_time"';
```

### Health Checks

Health check endpoint is available:

```bash
curl http://localhost/health
# Response: OK
```

## Performance Tuning

### Worker Configuration

Adjust in `nginx.conf` based on your server:

```nginx
worker_processes auto;  # Usually = number of CPU cores
worker_rlimit_nofile 65535;  # Max open files per worker

events {
    worker_connections 4096;  # Max connections per worker
}
```

### Keepalive Connections

Upstream keepalive reduces connection overhead:

```nginx
upstream api_gateway {
    server api-gateway:4000;
    keepalive 32;  # Maintain 32 idle connections
}
```

### Buffer Sizes

Adjust buffer sizes for larger requests/responses:

```nginx
proxy_buffer_size 4k;
proxy_buffers 8 4k;
proxy_busy_buffers_size 8k;
client_max_body_size 20M;  # Max upload size
```

## Troubleshooting

### Common Issues

**Issue: 502 Bad Gateway**
```bash
# Check if upstream services are running
docker-compose ps

# Check NGINX error logs
docker-compose logs nginx
```

**Issue: SSL Certificate Errors**
```bash
# Verify certificate files exist
ls -la /etc/nginx/ssl/

# Test SSL configuration
openssl s_client -connect yourdomain.com:443
```

**Issue: Rate Limiting**
```bash
# Check if you're being rate limited
curl -i http://localhost/api/v1/health

# Adjust rate limits in nginx.conf
```

**Issue: WebSocket Connection Failed**
```bash
# Ensure upgrade headers are set
# Check nginx error log for websocket errors
docker-compose logs nginx | grep -i websocket
```

### Testing Configuration

```bash
# Test NGINX config syntax
docker-compose -f docker-compose.nginx.yml exec nginx nginx -t

# Reload without downtime
docker-compose -f docker-compose.nginx.yml exec nginx nginx -s reload
```

## Environment Variables

Create a `.env` file in the `devops` directory:

```env
JWT_SECRET=your_super_secure_jwt_secret_here
DATABASE_URL=postgresql://postgres:postgres@postgres:5432/app_database
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=app_database
```

## Best Practices

1. **Always use HTTPS in production** - Don't skip SSL/TLS setup
2. **Enable HTTP/2** - Already configured for better performance
3. **Use rate limiting** - Protects against abuse and DDoS
4. **Monitor logs** - Set up log aggregation (ELK, Datadog, etc.)
5. **Regular updates** - Keep NGINX and SSL certificates up to date
6. **Backup configuration** - Version control all config files
7. **Test before deploy** - Always run `nginx -t` before reloading
8. **Use environment-specific configs** - Separate dev/staging/prod configs

## Integration with Kubernetes

For Kubernetes deployments, the NGINX configurations can be adapted to Ingress resources. See `devops/k8s/` directory for Kubernetes-specific configurations.

## Additional Resources

- [NGINX Documentation](https://nginx.org/en/docs/)
- [Let's Encrypt Documentation](https://letsencrypt.org/docs/)
- [Mozilla SSL Configuration Generator](https://ssl-config.mozilla.org/)
- [NGINX Performance Tuning](https://www.nginx.com/blog/tuning-nginx/)

## Support

For issues or questions about this configuration:
1. Check the troubleshooting section above
2. Review NGINX error logs
3. Verify all environment variables are set correctly
4. Ensure all services are running and healthy
