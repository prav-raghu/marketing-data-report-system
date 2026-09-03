variable "region" {
  description = "AWS region for all resources"
  type        = string
  default     = "af-south-1"

  validation {
    condition     = contains(["af-south-1", "eu-west-1", "us-east-1", "us-west-2", "ap-southeast-1"], var.region)
    error_message = "Region must be a supported AWS region."
  }
}

variable "environment" {
  description = "Deployment environment"
  type        = string

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be dev, staging, or prod."
  }
}

variable "project_name" {
  description = "Short project identifier used in resource names"
  type        = string
  default     = "node-mono-repo-template"
}

variable "customer_api_image" {
  description = "Full ECR image URI for customer-api"
  type        = string
}

variable "admin_api_image" {
  description = "Full ECR image URI for admin-api"
  type        = string
}

variable "customer_web_image" {
  description = "Full ECR image URI for customer-web (Next.js)"
  type        = string
}

variable "database_url" {
  description = "PostgreSQL connection string (Supabase pooler URL)"
  type        = string
  sensitive   = true
}

variable "mailtrap_api_key" {
  description = "Mailtrap transactional email API key"
  type        = string
  sensitive   = true
}

variable "mailtrap_from" {
  description = "Sender email address for transactional emails"
  type        = string
}

variable "mailtrap_from_name" {
  description = "Sender display name for transactional emails"
  type        = string
  default     = ""
}

variable "stripe_secret_key" {
  description = "Stripe secret key for payment processing"
  type        = string
  sensitive   = true
}

variable "stripe_webhook_secret" {
  description = "Stripe webhook signing secret"
  type        = string
  sensitive   = true
}

variable "stripe_publishable_key" {
  description = "Stripe publishable key (baked into customer-web env)"
  type        = string
}

variable "two_factor_encryption_key" {
  description = "64 hex-char key for 2FA TOTP encryption (admin-api only)"
  type        = string
  sensitive   = true
}

variable "smsportal_client_id" {
  description = "SMSPortal API Key Client ID for SA SMS notifications"
  type        = string
  sensitive   = true
  default     = ""
}

variable "smsportal_api_secret" {
  description = "SMSPortal API Key Secret for SA SMS notifications"
  type        = string
  sensitive   = true
  default     = ""
}

variable "customer_api_cpu" {
  description = "Fargate CPU units for customer-api (256 = 0.25 vCPU)"
  type        = number
  default     = 256
}

variable "customer_api_memory" {
  description = "Fargate memory (MiB) for customer-api"
  type        = number
  default     = 512
}

variable "customer_api_min_capacity" {
  description = "Minimum ECS task count for customer-api"
  type        = number
  default     = 1
}

variable "customer_api_max_capacity" {
  description = "Maximum ECS task count for customer-api"
  type        = number
  default     = 10
}

variable "admin_api_cpu" {
  description = "Fargate CPU units for admin-api"
  type        = number
  default     = 256
}

variable "admin_api_memory" {
  description = "Fargate memory (MiB) for admin-api"
  type        = number
  default     = 512
}

variable "admin_api_min_capacity" {
  description = "Minimum ECS task count for admin-api"
  type        = number
  default     = 1
}

variable "admin_api_max_capacity" {
  description = "Maximum ECS task count for admin-api"
  type        = number
  default     = 5
}

variable "customer_web_cpu" {
  description = "Fargate CPU units for customer-web"
  type        = number
  default     = 256
}

variable "customer_web_memory" {
  description = "Fargate memory (MiB) for customer-web"
  type        = number
  default     = 512
}

variable "customer_web_min_capacity" {
  description = "Minimum ECS task count for customer-web"
  type        = number
  default     = 1
}

variable "customer_web_max_capacity" {
  description = "Maximum ECS task count for customer-web"
  type        = number
  default     = 10
}

variable "redis_node_type" {
  description = "ElastiCache node type for Redis"
  type        = string
  default     = "cache.t3.micro"
}

variable "redis_num_cache_nodes" {
  description = "Number of ElastiCache nodes (1 = no replication)"
  type        = number
  default     = 1
}

variable "customer_web_url" {
  description = "Public URL of customer-web — used in customer-api CORS_ORIGIN"
  type        = string
  default     = ""
}

variable "admin_web_url" {
  description = "Public URL of admin-web — used in admin-api CORS_ORIGIN"
  type        = string
  default     = ""
}

variable "admin_web_domain" {
  description = "Custom domain for admin-web CloudFront distribution (leave empty to use CF domain)"
  type        = string
  default     = ""
}

variable "certificate_arn" {
  description = "ACM certificate ARN for HTTPS listeners (must be in us-east-1 for CloudFront)"
  type        = string
  default     = ""
}
