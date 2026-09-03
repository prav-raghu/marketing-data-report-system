output "hostname" {
  description = "Redis private hostname"
  value       = azurerm_redis_cache.redis.hostname
}

output "ssl_port" {
  description = "Redis TLS port"
  value       = azurerm_redis_cache.redis.ssl_port
}

output "primary_access_key" {
  description = "Redis primary access key"
  value       = azurerm_redis_cache.redis.primary_access_key
  sensitive   = true
}

output "redis_url" {
  description = "Redis TLS connection URL (rediss://)"
  value       = "rediss://:${azurerm_redis_cache.redis.primary_access_key}@${azurerm_redis_cache.redis.hostname}:${azurerm_redis_cache.redis.ssl_port}"
  sensitive   = true
}
