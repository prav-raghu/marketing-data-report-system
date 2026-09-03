# NGINX Proxy Quick Reference

## 🚀 Quick Start Commands

### Development Setup

```bash
# Windows PowerShell
.\infrastructure\nginx\setup.ps1

# Linux/Mac
./infrastructure/nginx/setup.sh

# Manual Docker Compose
cd devops
docker-compose -f docker-compose.nginx.yml up -d
```

### Access URLs (Development)

| Service | URL | Description |
|---------|-----|-------------|
| Customer Web | http://localhost/ | Customer-facing web app |
| Admin Web | http://localhost/admin/ | Admin dashboard |
| API Gateway | http://localhost/api/ | Main API endpoint |
| Health Check | http://localhost/health | Service health status |

## 📋 Common Commands

### Service Management

```bash
# Start all services
docker-compose -f docker-compose.nginx.yml up -d

# Stop all services
docker-compose -f docker-compose.nginx.yml down

# Restart NGINX only
docker-compose -f docker-compose.nginx.yml restart nginx

# View logs
docker-compose -f docker-compose.nginx.yml logs -f nginx

# Check service status
docker-compose -f docker-compose.nginx.yml ps
```

### NGINX Configuration

```bash
# Test configuration
docker-compose -f docker-compose.nginx.yml exec nginx nginx -t

# Reload without downtime
docker-compose -f docker-compose.nginx.yml exec nginx nginx -s reload

# View current configuration
docker-compose -f docker-compose.nginx.yml exec nginx cat /etc/nginx/nginx.conf
```

### SSL Certificate Management

```bash
# Initial certificate request (production)
docker-compose -f docker-compose.nginx.yml run --rm certbot certonly \
  --webroot \
  --webroot-path=/var/www/certbot \
  --email your-email@example.com \
  --agree-tos \
  -d api.yourdomain.com \
  -d app.yourdomain.com \
  -d admin.yourdomain.com

# Renew certificates
docker-compose -f docker-compose.nginx.yml run --rm certbot renew

# Check certificate expiry
docker-compose -f docker-compose.nginx.yml exec nginx \
  openssl x509 -in /etc/nginx/ssl/fullchain.pem -noout -dates
```

## 🔧 Configuration Files

### Main Files

| File | Purpose | When to Edit |
|------|---------|--------------|
| `nginx.conf` | Main NGINX config | Performance tuning, rate limits |
| `api-gateway.conf` | Production API proxy | Domain names, SSL paths |
| `customer-web.conf` | Customer web app | Domain names, caching |
| `admin-web.conf` | Admin web app | Domain names, auth |
| `development.conf` | Local dev config | Local development tweaks |

### Environment Variables

Create `.env` file in `devops/` directory:

```env
JWT_SECRET=your_jwt_secret
DATABASE_URL=postgresql://user:pass@postgres:5432/dbname
POSTGRES_USER=postgres
POSTGRES_PASSWORD=secure_password
POSTGRES_DB=app_database
```

## 🛠️ Customization

### Change Rate Limits

Edit `infrastructure/nginx/nginx.conf`:

```nginx
# Adjust these values
limit_req_zone $binary_remote_addr zone=api_limit:10m rate=100r/s;
limit_req_zone $binary_remote_addr zone=auth_limit:10m rate=10r/s;
```

### Update Domain Names (Production)

Replace `yourdomain.com` in:
- `infrastructure/nginx/api-gateway.conf`
- `infrastructure/nginx/customer-web.conf`
- `infrastructure/nginx/admin-web.conf`

### Add New Service

1. Add upstream in `nginx.conf`:
```nginx
upstream new_service {
    server new-service:3004;
    keepalive 32;
}
```

2. Add location block in appropriate config:
```nginx
location /api/v1/new/ {
    proxy_pass http://new_service;
    # ... proxy settings
}
```

### Enable API Caching

Add to location block:
```nginx
location /api/v1/public/ {
    proxy_cache api_cache;
    proxy_cache_valid 200 5m;
    proxy_cache_key "$scheme$request_method$host$request_uri";
    add_header X-Cache-Status $upstream_cache_status;
    
    proxy_pass http://api_gateway;
}
```

## 🐛 Troubleshooting

### 502 Bad Gateway

```bash
# Check if services are running
docker-compose -f docker-compose.nginx.yml ps

# Check service logs
docker-compose -f docker-compose.nginx.yml logs api-gateway

# Restart problematic service
docker-compose -f docker-compose.nginx.yml restart api-gateway
```

### SSL Certificate Issues

```bash
# Check certificate files
docker-compose -f docker-compose.nginx.yml exec nginx ls -la /etc/nginx/ssl/

# View certificate details
docker-compose -f docker-compose.nginx.yml exec nginx \
  openssl x509 -in /etc/nginx/ssl/fullchain.pem -text -noout

# Regenerate self-signed (dev only)
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout infrastructure/nginx/ssl/privkey.pem \
  -out infrastructure/nginx/ssl/fullchain.pem \
  -subj "/CN=localhost"
```

### Rate Limiting Errors (429 Too Many Requests)

```bash
# Increase rate limits temporarily
# Edit nginx.conf and increase rate values
# Then reload: docker-compose exec nginx nginx -s reload

# Check current rate limit status
docker-compose -f docker-compose.nginx.yml logs nginx | grep "limiting requests"
```

### WebSocket Connection Failed

```bash
# Verify upgrade headers in NGINX logs
docker-compose -f docker-compose.nginx.yml logs nginx | grep -i upgrade

# Test WebSocket endpoint
wscat -c ws://localhost/socket.io/

# Check if Socket.IO is enabled in backend
docker-compose -f docker-compose.nginx.yml logs api-gateway | grep -i socket
```

## 📊 Monitoring

### View Access Logs

```bash
# Real-time access log
docker-compose -f docker-compose.nginx.yml logs -f nginx

# Filter by status code
docker-compose -f docker-compose.nginx.yml logs nginx | grep " 500 "

# View only errors
docker-compose -f docker-compose.nginx.yml logs nginx | grep -i error
```

### Performance Metrics

Access logs include timing information:
- `rt` - Request time (total)
- `uct` - Upstream connect time
- `uht` - Upstream header time
- `urt` - Upstream response time

```bash
# Extract slow requests (>1 second)
docker-compose -f docker-compose.nginx.yml logs nginx | \
  grep -E "rt=[1-9][0-9]*\.[0-9]+"
```

### Health Check

```bash
# Quick health check
curl http://localhost/health

# Detailed service health
curl http://localhost/api/health

# Check from outside
curl https://api.yourdomain.com/health
```

## 🔒 Security

### Enable Admin Authentication

```bash
# Create htpasswd file
docker run --rm httpd:alpine htpasswd -Bbn admin yourpassword > \
  infrastructure/nginx/.htpasswd

# Restart NGINX
docker-compose -f docker-compose.nginx.yml restart nginx
```

### Update Security Headers

Edit service conf files to add/modify headers:

```nginx
add_header Strict-Transport-Security "max-age=31536000" always;
add_header X-Frame-Options "DENY" always;
add_header Content-Security-Policy "default-src 'self'" always;
```

### Restrict Access by IP

```nginx
location /admin/ {
    allow 192.168.1.0/24;
    deny all;
    # ... rest of config
}
```

## 📦 Production Deployment Checklist

- [ ] Update domain names in all `.conf` files
- [ ] Create `.env` file with production values
- [ ] Generate SSL certificates with Let's Encrypt
- [ ] Set up admin HTTP authentication
- [ ] Configure firewall (allow 80, 443)
- [ ] Set appropriate rate limits
- [ ] Enable access log aggregation
- [ ] Set up monitoring alerts
- [ ] Test SSL configuration (https://www.ssllabs.com/ssltest/)
- [ ] Verify CORS settings in backend services
- [ ] Test all service endpoints
- [ ] Configure automated backups

## 📚 Additional Resources

- [Full NGINX Documentation](./infrastructure/nginx/README.md)
- [Docker Compose Reference](https://docs.docker.com/compose/)
- [NGINX Official Docs](https://nginx.org/en/docs/)
- [Let's Encrypt Guide](https://letsencrypt.org/docs/)

---

**Need Help?** Check the [full NGINX README](./infrastructure/nginx/README.md) for detailed documentation.
