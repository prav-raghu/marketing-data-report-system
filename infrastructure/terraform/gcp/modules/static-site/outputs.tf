output "bucket_name" {
  description = "GCS bucket name — upload dist/ build artifacts here"
  value       = google_storage_bucket.spa.name
}

output "cdn_ip" {
  description = "Global CDN IP address — point your DNS A record here"
  value       = google_compute_global_address.cdn_ip.address
}

output "bucket_url" {
  description = "Direct GCS website URL (pre-CDN)"
  value       = google_storage_bucket.spa.url
}
