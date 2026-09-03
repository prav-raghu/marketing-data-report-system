variable "location" {
  description = "Azure region"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Base project name"
  type        = string
}

variable "resource_group_name" {
  description = "Name of the shared resource group"
  type        = string
}

variable "domain" {
  description = "Custom domain for the CDN endpoint — leave empty to skip"
  type        = string
  default     = ""
}
