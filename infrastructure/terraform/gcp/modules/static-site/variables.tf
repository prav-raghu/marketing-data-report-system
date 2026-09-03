variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region (used for backend bucket)"
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

variable "domain" {
  description = "Custom domain for the admin SPA (e.g. admin.example.com). Leave empty to use CDN IP."
  type        = string
  default     = ""
}
