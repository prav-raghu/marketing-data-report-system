variable "project_name" {
  description = "Short project identifier"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "region" {
  description = "AWS region"
  type        = string
}

variable "service_name" {
  description = "Service identifier (e.g. customer-api)"
  type        = string
}

variable "image" {
  description = "Full ECR image URI including tag"
  type        = string
}

variable "cpu" {
  description = "Fargate CPU units (256 = 0.25 vCPU)"
  type        = number
  default     = 256
}

variable "memory" {
  description = "Fargate memory in MiB"
  type        = number
  default     = 512
}

variable "port" {
  description = "Container port the service listens on"
  type        = number
  default     = 8080
}

variable "min_capacity" {
  description = "Minimum number of ECS tasks (ECS Fargate does not scale to zero automatically)"
  type        = number
  default     = 1
}

variable "max_capacity" {
  description = "Maximum number of ECS tasks"
  type        = number
  default     = 10
}

variable "cluster_id" {
  description = "ECS cluster ID"
  type        = string
}

variable "cluster_name" {
  description = "ECS cluster name"
  type        = string
}

variable "vpc_id" {
  description = "VPC ID"
  type        = string
}

variable "private_subnet_ids" {
  description = "Private subnet IDs for ECS tasks"
  type        = list(string)
}

variable "public_subnet_ids" {
  description = "Public subnet IDs for the ALB"
  type        = list(string)
}

variable "alb_security_group_id" {
  description = "Security group ID for the ALB"
  type        = string
}

variable "ecs_security_group_id" {
  description = "Security group ID for ECS tasks"
  type        = string
}

variable "env_vars" {
  description = "Plain-text environment variables to inject into the container"
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "Map of ENV_VAR_NAME -> Secrets Manager secret ARN"
  type        = map(string)
  default     = {}
  sensitive   = true
}

variable "health_check_path" {
  description = "HTTP path for ALB health checks"
  type        = string
  default     = "/health"
}

variable "certificate_arn" {
  description = "ACM certificate ARN for HTTPS listener (leave empty for HTTP only)"
  type        = string
  default     = ""
}
