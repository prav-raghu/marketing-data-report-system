resource "google_storage_bucket" "spa" {
  name                        = "${var.project_name}-${var.environment}-admin-web"
  location                    = "EU"
  project                     = var.project_id
  force_destroy               = var.environment != "prod"
  uniform_bucket_level_access = true

  website {
    main_page_suffix = "index.html"
    not_found_page   = "index.html"
  }

  cors {
    origin          = ["*"]
    method          = ["GET", "HEAD", "OPTIONS"]
    response_header = ["*"]
    max_age_seconds = 3600
  }

  labels = {
    project     = var.project_name
    environment = var.environment
    managed-by  = "terraform"
  }
}

resource "google_storage_bucket_iam_member" "public_read" {
  bucket = google_storage_bucket.spa.name
  role   = "roles/storage.objectViewer"
  member = "allUsers"
}

resource "google_compute_backend_bucket" "cdn" {
  name        = "${var.project_name}-${var.environment}-admin-cdn"
  description = "Node Mono Repo Template Admin Web CDN backend"
  bucket_name = google_storage_bucket.spa.name
  project     = var.project_id
  enable_cdn  = true

  cdn_policy {
    cache_mode        = "CACHE_ALL_STATIC"
    default_ttl       = 3600
    max_ttl           = 86400
    negative_caching  = true
    serve_while_stale = 86400
  }
}

resource "google_compute_global_address" "cdn_ip" {
  name    = "${var.project_name}-${var.environment}-admin-cdn-ip"
  project = var.project_id
}

resource "google_compute_url_map" "cdn" {
  name            = "${var.project_name}-${var.environment}-admin-url-map"
  project         = var.project_id
  default_service = google_compute_backend_bucket.cdn.id
}

resource "google_compute_target_https_proxy" "cdn" {
  count = var.domain != "" ? 1 : 0

  name             = "${var.project_name}-${var.environment}-admin-https-proxy"
  project          = var.project_id
  url_map          = google_compute_url_map.cdn.id
  ssl_certificates = [google_compute_managed_ssl_certificate.cdn[0].id]
}

resource "google_compute_managed_ssl_certificate" "cdn" {
  count = var.domain != "" ? 1 : 0

  name    = "${var.project_name}-${var.environment}-admin-cert"
  project = var.project_id

  managed {
    domains = [var.domain]
  }
}

resource "google_compute_target_http_proxy" "cdn_http" {
  name    = "${var.project_name}-${var.environment}-admin-http-proxy"
  project = var.project_id
  url_map = google_compute_url_map.cdn.id
}

resource "google_compute_global_forwarding_rule" "https" {
  count = var.domain != "" ? 1 : 0

  name       = "${var.project_name}-${var.environment}-admin-https"
  project    = var.project_id
  target     = google_compute_target_https_proxy.cdn[0].id
  port_range = "443"
  ip_address = google_compute_global_address.cdn_ip.address
}

resource "google_compute_global_forwarding_rule" "http" {
  name       = "${var.project_name}-${var.environment}-admin-http"
  project    = var.project_id
  target     = google_compute_target_http_proxy.cdn_http.id
  port_range = "80"
  ip_address = google_compute_global_address.cdn_ip.address
}
