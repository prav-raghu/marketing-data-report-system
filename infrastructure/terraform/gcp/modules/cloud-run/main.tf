resource "google_cloud_run_v2_service" "service" {
  name     = "${var.project_name}-${var.environment}-${var.service_name}"
  location = var.region
  project  = var.project_id

  labels = {
    project     = var.project_name
    environment = var.environment
    managed-by  = "terraform"
    service     = var.service_name
  }

  template {
    service_account = var.service_account_email

    scaling {
      min_instance_count = var.min_instances
      max_instance_count = var.max_instances
    }

    vpc_access {
      connector = var.serverless_connector_id
      egress    = "PRIVATE_RANGES_ONLY"
    }

    containers {
      image = var.image

      ports {
        container_port = var.port
      }

      resources {
        limits = {
          cpu    = var.cpu
          memory = var.memory
        }
        cpu_idle          = true
        startup_cpu_boost = true
      }

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
          name = env.key
          value_source {
            secret_key_ref {
              secret  = env.value
              version = "latest"
            }
          }
        }
      }

      startup_probe {
        http_get {
          path = "/health"
          port = var.port
        }
        initial_delay_seconds = 5
        period_seconds        = 5
        failure_threshold     = 3
        timeout_seconds       = 3
      }

      liveness_probe {
        http_get {
          path = "/health"
          port = var.port
        }
        period_seconds    = 30
        failure_threshold = 3
        timeout_seconds   = 5
      }
    }

    max_instance_request_concurrency = var.concurrency

    annotations = {
      "autoscaling.knative.dev/minScale" = tostring(var.min_instances)
      "autoscaling.knative.dev/maxScale" = tostring(var.max_instances)
    }
  }

  traffic {
    type    = "TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST"
    percent = 100
  }
}

resource "google_cloud_run_v2_service_iam_member" "public_invoker" {
  count = var.allow_public_access ? 1 : 0

  project  = var.project_id
  location = var.region
  name     = google_cloud_run_v2_service.service.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
