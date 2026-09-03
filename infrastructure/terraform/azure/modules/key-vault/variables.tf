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

variable "secrets" {
  description = "Map of secret name to secret value to store in Key Vault"
  type        = map(string)
  sensitive   = true
  default     = {}
}
