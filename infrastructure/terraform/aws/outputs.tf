output "customer_api_url" {
  description = "ALB URL for customer-api"
  value       = module.customer_api.service_url
}

output "admin_api_url" {
  description = "ALB URL for admin-api"
  value       = module.admin_api.service_url
}

output "customer_web_url" {
  description = "ALB URL for customer-web (Next.js)"
  value       = module.customer_web.service_url
}

output "admin_web_cloudfront_domain" {
  description = "CloudFront domain for admin-web SPA — create DNS CNAME pointing here"
  value       = module.admin_web.cloudfront_domain
}

output "admin_web_bucket_name" {
  description = "S3 bucket for admin-web — upload dist/ here after every build"
  value       = module.admin_web.bucket_name
}

output "admin_web_cloudfront_distribution_id" {
  description = "CloudFront distribution ID — use for cache invalidation after deploy"
  value       = module.admin_web.cloudfront_distribution_id
}

output "ecr_customer_api_url" {
  description = "ECR repository URI for customer-api"
  value       = module.ecr_customer_api.repository_url
}

output "ecr_admin_api_url" {
  description = "ECR repository URI for admin-api"
  value       = module.ecr_admin_api.repository_url
}

output "ecr_customer_web_url" {
  description = "ECR repository URI for customer-web"
  value       = module.ecr_customer_web.repository_url
}

output "ecs_cluster_name" {
  description = "ECS cluster name"
  value       = aws_ecs_cluster.main.name
}
