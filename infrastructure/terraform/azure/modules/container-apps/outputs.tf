output "service_url" {
  description = "HTTPS ingress URL for the Container App"
  value       = "https://${azurerm_container_app.service.ingress[0].fqdn}"
}

output "fqdn" {
  description = "Container App ingress FQDN"
  value       = azurerm_container_app.service.ingress[0].fqdn
}

output "service_name" {
  description = "Container App resource name"
  value       = azurerm_container_app.service.name
}

output "principal_id" {
  description = "System-assigned managed identity principal ID"
  value       = azurerm_container_app.service.identity[0].principal_id
}
