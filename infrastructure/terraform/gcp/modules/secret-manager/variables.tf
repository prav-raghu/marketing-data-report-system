variable "project_id" {
  description = "GCP project ID"
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

variable "secrets" {
  description = "Map of secret name -> secret value. All values are stored as Secret Manager secrets."
  type        = map(string)
  sensitive   = true
}
