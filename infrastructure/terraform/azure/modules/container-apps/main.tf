resource "azurerm_container_app" "service" {
  name                         = "${var.project_name}-${var.environment}-${var.service_name}"
  container_app_environment_id = var.container_app_environment_id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  identity {
    type = "SystemAssigned"
  }

  dynamic "secret" {
    for_each = var.secret_env_vars
    content {
      name                = lower(replace(secret.key, "_", "-"))
      key_vault_secret_id = secret.value
      identity            = "System"
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = var.service_name
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      dynamic "env" {
        for_each = var.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.secret_env_vars
        content {
          name        = env.key
          secret_name = lower(replace(env.key, "_", "-"))
        }
      }

      liveness_probe {
        path             = "/health"
        port             = var.port
        transport        = "HTTP"
        initial_delay    = 30
        interval_seconds = 30
      }

      readiness_probe {
        path             = "/health"
        port             = var.port
        transport        = "HTTP"
        interval_seconds = 15
      }

      startup_probe {
        path             = "/health"
        port             = var.port
        transport        = "HTTP"
        interval_seconds = 10
      }
    }

    http_scale_rule {
      name                = "http-scaling"
      concurrent_requests = "10"
    }
  }

  ingress {
    external_enabled = var.allow_public_access
    target_port      = var.port

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "azurerm_role_assignment" "kv_secrets_user" {
  for_each = var.secret_env_vars

  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_container_app.service.identity[0].principal_id
}
