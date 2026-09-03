variable "project_name" {
  description = "Short project identifier"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "service_name" {
  description = "Service identifier (e.g. customer-api)"
  type        = string
}
