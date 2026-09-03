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

variable "vpc_cidr" {
  description = "Primary CIDR range for the VPC subnet"
  type        = string
  default     = "10.0.0.0/20"
}

variable "serverless_connector_cidr" {
  description = "CIDR range for the Serverless VPC Access connector (must be /28)"
  type        = string
  default     = "10.8.0.0/28"
}
