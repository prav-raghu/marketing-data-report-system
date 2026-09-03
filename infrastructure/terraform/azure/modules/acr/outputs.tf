output "login_server" {
  description = "ACR login server hostname (e.g. acrdev.azurecr.io)"
  value       = azurerm_container_registry.acr.login_server
}

output "registry_name" {
  description = "ACR registry name"
  value       = azurerm_container_registry.acr.name
}

output "admin_username" {
  description = "ACR admin username"
  value       = azurerm_container_registry.acr.admin_username
  sensitive   = true
}

output "admin_password" {
  description = "ACR admin password"
  value       = azurerm_container_registry.acr.admin_password
  sensitive   = true
}
