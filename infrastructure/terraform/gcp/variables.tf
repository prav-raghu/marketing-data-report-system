variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region for all resources"
  type        = string
  default     = "africa-south1"

  validation {
    condition     = contains(["africa-south1", "europe-west1", "us-central1", "us-east1", "asia-southeast1"], var.region)
    error_message = "Region must be a supported GCP region."
  }
}

variable "environment" {
  description = "Deployment environment"
  type        = string

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be dev, staging, or prod."
  }
}

variable "project_name" {
  description = "Short project identifier used in resource names"
  type        = string
  default     = "node-mono-repo-template"
}

variable "customer_api_image" {
  description = "Full Artifact Registry image URI for customer-api"
  type        = string
}

variable "admin_api_image" {
  description = "Full Artifact Registry image URI for admin-api"
  type        = string
}

variable "customer_web_image" {
  description = "Full Artifact Registry image URI for customer-web (Next.js)"
  type        = string
}

variable "database_url" {
  description = "PostgreSQL connection string (Supabase pooler URL)"
  type        = string
  sensitive   = true
}

variable "mailtrap_api_key" {
  description = "Mailtrap transactional email API key"
  type        = string
  sensitive   = true
}

variable "mailtrap_from" {
  description = "Sender email address e.g. noreply@example.com"
  type        = string
}

variable "mailtrap_from_name" {
  description = "Sender display name for transactional emails"
  type        = string
  default     = ""
}

variable "stripe_secret_key" {
  description = "Stripe secret key for payment processing"
  type        = string
  sensitive   = true
}

variable "stripe_webhook_secret" {
  description = "Stripe webhook signing secret"
  type        = string
  sensitive   = true
}

variable "stripe_publishable_key" {
  description = "Stripe publishable key (baked into customer-web build)"
  type        = string
}

variable "two_factor_encryption_key" {
  description = "64 hex-char key for 2FA TOTP encryption (admin-api only)"
  type        = string
  sensitive   = true
}

variable "smsportal_client_id" {
  description = "SMSPortal API Key Client ID for SA SMS notifications"
  type        = string
  sensitive   = true

  default = ""
}

variable "smsportal_api_secret" {
  description = "SMSPortal API Key Secret for SA SMS notifications"
  type        = string
  sensitive   = true

  default = ""
}

variable "customer_api_min_instances" {
  description = "Minimum Cloud Run instances for customer-api (0 = scale to zero)"
  type        = number
  default     = 0
}

variable "customer_api_max_instances" {
  description = "Maximum Cloud Run instances for customer-api"
  type        = number
  default     = 10
}

variable "admin_api_min_instances" {
  description = "Minimum Cloud Run instances for admin-api"
  type        = number
  default     = 0
}

variable "admin_api_max_instances" {
  description = "Maximum Cloud Run instances for admin-api"
  type        = number
  default     = 5
}

variable "customer_web_min_instances" {
  description = "Minimum Cloud Run instances for customer-web"
  type        = number
  default     = 0
}

variable "customer_web_max_instances" {
  description = "Maximum Cloud Run instances for customer-web"
  type        = number
  default     = 10
}

variable "redis_memory_size_gb" {
  description = "Redis Memorystore instance memory in GB"
  type        = number
  default     = 1
}

variable "redis_tier" {
  description = "Redis tier: BASIC or STANDARD_HA"
  type        = string
  default     = "BASIC"

  validation {
    condition     = contains(["BASIC", "STANDARD_HA"], var.redis_tier)
    error_message = "Redis tier must be BASIC or STANDARD_HA."
  }
}

variable "customer_web_url" {
  description = "Public URL of customer-web — used in customer-api CORS_ORIGIN"
  type        = string
  default     = ""
}

variable "admin_web_url" {
  description = "Public URL of admin-web — used in admin-api CORS_ORIGIN"
  type        = string
  default     = ""
}

variable "admin_web_domain" {
  description = "Custom domain for admin-web CDN (leave empty to use GCP CDN IP directly)"
  type        = string
  default     = ""
}
