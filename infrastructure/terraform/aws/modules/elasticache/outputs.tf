output "primary_endpoint" {
  description = "Redis primary endpoint hostname"
  value       = aws_elasticache_replication_group.redis.primary_endpoint_address
}

output "port" {
  description = "Redis port"
  value       = 6379
}

output "redis_url" {
  description = "Full Redis connection URL (rediss:// with TLS)"
  value       = "rediss://${aws_elasticache_replication_group.redis.primary_endpoint_address}:6379"
  sensitive   = true
}

output "auth_token" {
  description = "Redis AUTH token"
  value       = aws_elasticache_replication_group.redis.auth_token
  sensitive   = true
}
