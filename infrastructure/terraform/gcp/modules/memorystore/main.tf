resource "google_redis_instance" "cache" {
  name           = "${var.project_name}-${var.environment}-redis"
  tier           = var.tier
  memory_size_gb = var.memory_size_gb
  region         = var.region
  project        = var.project_id

  redis_version    = var.redis_version
  display_name     = "Node Mono Repo Template ${var.environment} Redis"
  authorized_network = var.vpc_network

  transit_encryption_mode = "SERVER_AUTHENTICATION"
  auth_enabled            = true

  redis_configs = {
    maxmemory-policy = "allkeys-lru"
    notify-keyspace-events = ""
  }

  labels = {
    project     = var.project_name
    environment = var.environment
    managed-by  = "terraform"
  }

  maintenance_policy {
    weekly_maintenance_window {
      day = "SUNDAY"
      start_time {
        hours   = 2
        minutes = 0
        seconds = 0
        nanos   = 0
      }
    }
  }
}
