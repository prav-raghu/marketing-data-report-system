output "storage_account_name" {
  description = "Storage account name — upload dist/ contents here"
  value       = azurerm_storage_account.spa.name
}

output "primary_web_host" {
  description = "Static website blob endpoint hostname"
  value       = azurerm_storage_account.spa.primary_web_host
}

output "cdn_endpoint_hostname" {
  description = "CDN endpoint hostname — create DNS CNAME pointing here"
  value       = azurerm_cdn_endpoint.spa.host_name
}

output "cdn_endpoint_id" {
  description = "CDN endpoint resource ID — used for cache purge"
  value       = azurerm_cdn_endpoint.spa.id
}

output "cdn_profile_name" {
  description = "CDN profile name"
  value       = azurerm_cdn_profile.spa.name
}
