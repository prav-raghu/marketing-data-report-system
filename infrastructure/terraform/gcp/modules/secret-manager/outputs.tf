output "secret_ids" {
  description = "Map of secret name -> Secret Manager resource ID (for use in Cloud Run secret_env_vars)"
  value = {
    for k, v in google_secret_manager_secret.secrets : k => v.secret_id
  }
  sensitive = true
}
