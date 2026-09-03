resource "google_artifact_registry_repository" "images" {
  repository_id = "${var.project_name}-${var.environment}"
  format        = "DOCKER"
  location      = var.region
  project       = var.project_id
  description   = "Node Mono Repo Template ${var.environment} container images"

  labels = {
    project     = var.project_name
    environment = var.environment
    managed-by  = "terraform"
  }

  cleanup_policies {
    id     = "keep-minimum-versions"
    action = "KEEP"
    most_recent_versions {
      keep_count = 10
    }
  }

  cleanup_policies {
    id     = "delete-old-untagged"
    action = "DELETE"
    condition {
      tag_state  = "UNTAGGED"
      older_than = "604800s"
    }
  }
}

resource "google_artifact_registry_repository_iam_member" "cloud_run_reader" {
  repository = google_artifact_registry_repository.images.name
  location   = var.region
  project    = var.project_id
  role       = "roles/artifactregistry.reader"
  member     = "serviceAccount:${data.google_project.project.number}-compute@developer.gserviceaccount.com"
}

data "google_project" "project" {
  project_id = var.project_id
}
