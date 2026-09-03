output "vpc_id" {
  description = "VPC network self-link"
  value       = google_compute_network.vpc.self_link
}

output "vpc_name" {
  description = "VPC network name"
  value       = google_compute_network.vpc.name
}

output "subnet_id" {
  description = "Private subnet self-link"
  value       = google_compute_subnetwork.private.self_link
}

output "serverless_connector_id" {
  description = "Serverless VPC Access connector ID (used by Cloud Run)"
  value       = google_vpc_access_connector.serverless.id
}
