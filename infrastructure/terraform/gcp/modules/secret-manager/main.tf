resource "google_secret_manager_secret" "secrets" {
  for_each  = var.secrets
  secret_id = "${var.project_name}-${var.environment}-${replace(lower(each.key), "_", "-")}"
  project   = var.project_id

  labels = {
    project     = var.project_name
    environment = var.environment
    managed-by  = "terraform"
  }

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "versions" {
  for_each    = var.secrets
  secret      = google_secret_manager_secret.secrets[each.key].id
  secret_data = each.value
}
