output "vault_id" {
  description = "Key Vault resource ID"
  value       = azurerm_key_vault.main.id
}

output "vault_uri" {
  description = "Key Vault URI"
  value       = azurerm_key_vault.main.vault_uri
}

output "secret_ids" {
  description = "Map of original secret name to Key Vault secret versioned ID"
  value       = { for k, v in azurerm_key_vault_secret.secrets : k => v.id }
  sensitive   = true
}

output "secret_versionless_ids" {
  description = "Map of original secret name to Key Vault secret versionless ID (for Container Apps)"
  value       = { for k, v in azurerm_key_vault_secret.secrets : k => v.versionless_id }
  sensitive   = true
}
