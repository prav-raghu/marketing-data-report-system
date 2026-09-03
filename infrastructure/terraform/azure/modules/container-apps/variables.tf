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

variable "service_name" {
  description = "Short name for this service (e.g. customer-api)"
  type        = string
}

variable "image" {
  description = "Full ACR image URI including tag"
  type        = string
}

variable "port" {
  description = "Container port to expose"
  type        = number
}

variable "container_app_environment_id" {
  description = "ID of the Container Apps Environment"
  type        = string
}

variable "min_replicas" {
  description = "Minimum number of replicas (0 = scale-to-zero)"
  type        = number
  default     = 0
}

variable "max_replicas" {
  description = "Maximum number of replicas"
  type        = number
  default     = 10
}

variable "cpu" {
  description = "CPU allocation per replica (e.g. 0.5, 1.0, 2.0)"
  type        = number
  default     = 0.5
}

variable "memory" {
  description = "Memory allocation per replica (e.g. 1Gi, 2Gi)"
  type        = string
  default     = "1Gi"
}

variable "env_vars" {
  description = "Plain environment variables"
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "Map of ENV_VAR_NAME to Key Vault secret URI for secrets injected at runtime"
  type        = map(string)
  sensitive   = true
  default     = {}
}

variable "key_vault_id" {
  description = "Resource ID of the Key Vault — used to assign Key Vault Secrets User role"
  type        = string
}

variable "allow_public_access" {
  description = "Expose an external HTTP(S) ingress endpoint"
  type        = bool
  default     = true
}
