variable "project_name" {
  description = "Short project identifier"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "secrets" {
  description = "Map of secret name -> secret value. Stored as individual Secrets Manager secrets."
  type        = map(string)
  sensitive   = true
}
