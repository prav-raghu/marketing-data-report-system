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

variable "vpc_cidr" {
  description = "Address space for the virtual network"
  type        = string
  default     = "10.0.0.0/16"
}
