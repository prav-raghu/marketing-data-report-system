variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Short project identifier"
  type        = string
}

variable "service_name" {
  description = "Cloud Run service name suffix (e.g. customer-api, admin-api, customer-web)"
  type        = string
}

variable "image" {
  description = "Full container image URI from Artifact Registry"
  type        = string
}

variable "port" {
  description = "Container port the application listens on"
  type        = number
  default     = 8080
}

variable "min_instances" {
  description = "Minimum number of container instances (0 = scale to zero)"
  type        = number
  default     = 0
}

variable "max_instances" {
  description = "Maximum number of container instances"
  type        = number
  default     = 10
}

variable "cpu" {
  description = "vCPU allocation per container instance"
  type        = string
  default     = "1"
}

variable "memory" {
  description = "Memory allocation per container instance"
  type        = string
  default     = "512Mi"
}

variable "env_vars" {
  description = "Non-sensitive environment variables as a map"
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "Environment variables sourced from Secret Manager. Map of ENV_VAR_NAME -> secret resource name"
  type        = map(string)
  default     = {}
}

variable "serverless_connector_id" {
  description = "Serverless VPC Access connector ID for private Redis access"
  type        = string
}

variable "service_account_email" {
  description = "Service account email for this Cloud Run service"
  type        = string
}

variable "allow_public_access" {
  description = "Whether to allow unauthenticated invocations (true for public-facing services)"
  type        = bool
  default     = true
}

variable "concurrency" {
  description = "Maximum concurrent requests per container instance"
  type        = number
  default     = 80
}
