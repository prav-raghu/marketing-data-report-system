output "customer_api_url" {
  description = "Cloud Run URL for customer-api"
  value       = module.customer_api.service_url
}

output "admin_api_url" {
  description = "Cloud Run URL for admin-api"
  value       = module.admin_api.service_url
}

output "customer_web_url" {
  description = "Cloud Run URL for customer-web (Next.js)"
  value       = module.customer_web.service_url
}

output "admin_web_cdn_ip" {
  description = "Global CDN IP for admin-web SPA — create DNS A record pointing here"
  value       = module.admin_web.cdn_ip
}

output "admin_web_bucket_name" {
  description = "GCS bucket for admin-web — upload dist/ here after every build"
  value       = module.admin_web.bucket_name
}

output "artifact_registry_url" {
  description = "Artifact Registry repository URL — prefix for all Docker image tags"
  value       = module.artifact_registry.repository_url
}
