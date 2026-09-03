variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "southafricanorth"

  validation {
    condition     = contains(["southafricanorth", "southafricawest", "westeurope", "eastus", "uksouth"], var.location)
    error_message = "location must be a valid Azure region."
  }
}

variable "environment" {
  description = "Deployment environment — dev, staging, or prod"
  type        = string

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be dev, staging, or prod."
  }
}

variable "project_name" {
  description = "Base project name used in all resource names"
  type        = string
  default     = "node-mono-repo-template"
}

variable "customer_api_image" {
  description = "Full ACR image URI for customer-api (e.g. acrdev.azurecr.io/customer-api:latest)"
  type        = string
}

variable "admin_api_image" {
  description = "Full ACR image URI for admin-api"
  type        = string
}

variable "customer_web_image" {
  description = "Full ACR image URI for customer-web (Next.js)"
  type        = string
}

variable "database_url" {
  description = "PostgreSQL connection string (Supabase)"
  type        = string
  sensitive   = true
}

variable "mailtrap_api_key" {
  description = "Mailtrap transactional email API key"
  type        = string
  sensitive   = true
}

variable "mailtrap_from" {
  description = "Sender email address for transactional emails"
  type        = string
}

variable "mailtrap_from_name" {
  description = "Sender display name for transactional emails"
  type        = string
  default     = ""
}

variable "stripe_secret_key" {
  description = "Stripe secret key"
  type        = string
  sensitive   = true
}

variable "stripe_webhook_secret" {
  description = "Stripe webhook signing secret"
  type        = string
  sensitive   = true
}

variable "stripe_publishable_key" {
  description = "Stripe publishable key (non-sensitive, injected as plain env var)"
  type        = string
}

variable "two_factor_encryption_key" {
  description = "64-character hex key for 2FA TOTP encryption"
  type        = string
  sensitive   = true
}

variable "smsportal_client_id" {
  description = "SMSPortal API Key Client ID for South Africa SMS"
  type        = string
  sensitive   = true
}

variable "smsportal_api_secret" {
  description = "SMSPortal API Key Secret for South Africa SMS"
  type        = string
  sensitive   = true
}

variable "customer_api_min_replicas" {
  description = "Minimum replicas for customer-api (0 = scale-to-zero)"
  type        = number
  default     = 0
}

variable "customer_api_max_replicas" {
  description = "Maximum replicas for customer-api"
  type        = number
  default     = 10
}

variable "admin_api_min_replicas" {
  description = "Minimum replicas for admin-api"
  type        = number
  default     = 0
}

variable "admin_api_max_replicas" {
  description = "Maximum replicas for admin-api"
  type        = number
  default     = 5
}

variable "customer_web_min_replicas" {
  description = "Minimum replicas for customer-web"
  type        = number
  default     = 0
}

variable "customer_web_max_replicas" {
  description = "Maximum replicas for customer-web"
  type        = number
  default     = 10
}

variable "redis_capacity" {
  description = "Azure Cache for Redis capacity (number of cache units)"
  type        = number
  default     = 1
}

variable "redis_family" {
  description = "Azure Cache for Redis family — C (Basic/Standard) or P (Premium)"
  type        = string
  default     = "C"

  validation {
    condition     = contains(["C", "P"], var.redis_family)
    error_message = "redis_family must be C or P."
  }
}

variable "redis_sku" {
  description = "Azure Cache for Redis SKU — Basic, Standard, or Premium"
  type        = string
  default     = "Standard"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.redis_sku)
    error_message = "redis_sku must be Basic, Standard, or Premium."
  }
}

variable "customer_web_url" {
  description = "Public URL of the customer-web app (used in CORS headers)"
  type        = string
  default     = ""
}

variable "admin_web_url" {
  description = "Public URL of the admin-web SPA (used in CORS headers)"
  type        = string
  default     = ""
}

variable "admin_web_domain" {
  description = "Custom domain for admin-web CDN endpoint (leave empty to skip)"
  type        = string
  default     = ""
}
