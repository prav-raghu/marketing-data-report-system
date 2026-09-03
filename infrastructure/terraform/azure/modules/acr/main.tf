locals {
  registry_name = replace("${var.project_name}${var.environment}", "-", "")
}

resource "azurerm_container_registry" "acr" {
  name                = local.registry_name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  admin_enabled       = true

  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}
