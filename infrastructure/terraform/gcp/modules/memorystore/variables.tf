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

variable "memory_size_gb" {
  description = "Redis instance memory in GB"
  type        = number
  default     = 1
}

variable "tier" {
  description = "Redis tier: BASIC or STANDARD_HA"
  type        = string
  default     = "BASIC"
}

variable "redis_version" {
  description = "Redis version"
  type        = string
  default     = "REDIS_7_0"
}

variable "vpc_network" {
  description = "VPC network self-link that Redis will be attached to"
  type        = string
}
