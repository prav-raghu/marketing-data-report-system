output "resource_group_name" {
  description = "Shared resource group name"
  value       = azurerm_resource_group.main.name
}

output "resource_group_location" {
  description = "Resource group location"
  value       = azurerm_resource_group.main.location
}

output "vnet_id" {
  description = "Virtual network ID"
  value       = azurerm_virtual_network.main.id
}

output "container_apps_subnet_id" {
  description = "Subnet ID delegated to Container Apps"
  value       = azurerm_subnet.container_apps.id
}

output "redis_subnet_id" {
  description = "Subnet ID for Redis private endpoint"
  value       = azurerm_subnet.redis.id
}

output "private_endpoints_subnet_id" {
  description = "Subnet ID for general private endpoints"
  value       = azurerm_subnet.private_endpoints.id
}

output "container_app_environment_id" {
  description = "Container Apps Environment resource ID"
  value       = azurerm_container_app_environment.main.id
}

output "container_app_environment_name" {
  description = "Container Apps Environment name"
  value       = azurerm_container_app_environment.main.name
}

output "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID"
  value       = azurerm_log_analytics_workspace.main.id
}
