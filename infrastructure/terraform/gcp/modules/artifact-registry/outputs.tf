output "repository_url" {
  description = "Artifact Registry repository URL — prefix for all image tags"
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.images.repository_id}"
}

output "repository_id" {
  description = "Artifact Registry repository ID"
  value       = google_artifact_registry_repository.images.repository_id
}
