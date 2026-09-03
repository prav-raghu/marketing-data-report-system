output "service_url" {
  description = "Public HTTPS URL of the Cloud Run service"
  value       = google_cloud_run_v2_service.service.uri
}

output "service_name" {
  description = "Full Cloud Run service name"
  value       = google_cloud_run_v2_service.service.name
}

output "latest_revision" {
  description = "Latest deployed revision name"
  value       = google_cloud_run_v2_service.service.latest_created_revision
}
