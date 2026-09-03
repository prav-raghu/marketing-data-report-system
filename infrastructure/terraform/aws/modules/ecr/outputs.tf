output "repository_url" {
  description = "ECR repository URI — used as image prefix when tagging and pushing"
  value       = aws_ecr_repository.repo.repository_url
}

output "repository_name" {
  description = "ECR repository name"
  value       = aws_ecr_repository.repo.name
}
