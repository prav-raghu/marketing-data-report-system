output "service_url" {
  description = "ALB DNS name — use as the public endpoint for this service"
  value       = "http${var.certificate_arn != "" ? "s" : ""}://${aws_lb.alb.dns_name}"
}

output "alb_dns_name" {
  description = "Raw ALB DNS name (for Route 53 ALIAS records)"
  value       = aws_lb.alb.dns_name
}

output "alb_zone_id" {
  description = "ALB hosted zone ID (for Route 53 ALIAS records)"
  value       = aws_lb.alb.zone_id
}

output "service_name" {
  description = "ECS service name"
  value       = aws_ecs_service.service.name
}
