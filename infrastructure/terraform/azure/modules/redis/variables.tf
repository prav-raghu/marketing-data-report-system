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

variable "vnet_id" {
  description = "ID of the virtual network for private endpoint"
  type        = string
}

variable "redis_subnet_id" {
  description = "ID of the subnet for the Redis private endpoint"
  type        = string
}

variable "capacity" {
  description = "Number of cache units"
  type        = number
  default     = 1
}

variable "family" {
  description = "Redis family — C or P"
  type        = string
  default     = "C"
}

variable "sku_name" {
  description = "Redis SKU — Basic, Standard, or Premium"
  type        = string
  default     = "Standard"
}
