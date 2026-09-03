resource "aws_elasticache_subnet_group" "redis" {
  name       = "${var.project_name}-${var.environment}-redis-subnet"
  subnet_ids = var.private_subnet_ids

  tags = {
    Name = "${var.project_name}-${var.environment}-redis-subnet"
  }
}

resource "aws_elasticache_replication_group" "redis" {
  replication_group_id = "${var.project_name}-${var.environment}-redis"
  description          = "Node Mono Repo Template ${var.environment} Redis"

  node_type            = var.node_type
  num_cache_clusters   = var.num_cache_nodes
  engine_version       = var.engine_version
  port                 = 6379
  parameter_group_name = "default.redis7"

  subnet_group_name  = aws_elasticache_subnet_group.redis.name
  security_group_ids = [var.redis_security_group_id]

  at_rest_encryption_enabled = true
  transit_encryption_enabled = true
  auth_token_enabled         = true

  automatic_failover_enabled = var.num_cache_nodes > 1

  maintenance_window       = "sun:02:00-sun:03:00"
  snapshot_retention_limit = 1
  snapshot_window          = "01:00-02:00"

  apply_immediately = var.num_cache_nodes == 1

  tags = {
    Name = "${var.project_name}-${var.environment}-redis"
  }
}
