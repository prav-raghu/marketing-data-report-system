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

variable "domain" {
  description = "Custom domain for the CloudFront distribution (leave empty to use CF-generated domain)"
  type        = string
  default     = ""
}

variable "certificate_arn" {
  description = "ACM certificate ARN (must be in us-east-1 for CloudFront). Required when domain is set."
  type        = string
  default     = ""
}
