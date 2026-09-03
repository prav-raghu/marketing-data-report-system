output "secret_arns" {
  description = "Map of secret name -> Secrets Manager ARN (used in ECS task definition secret_env_vars)"
  value = {
    for k, v in aws_secretsmanager_secret.secrets : k => v.arn
  }
  sensitive = true
}
