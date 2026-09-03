locals {
  storage_name = replace("${var.project_name}${var.environment}adminweb", "-", "")
}

resource "azurerm_storage_account" "spa" {
  name                     = local.storage_name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  static_website {
    index_document     = "index.html"
    error_404_document = "index.html"
  }

  blob_properties {
    cors_rule {
      allowed_headers    = ["*"]
      allowed_methods    = ["GET", "HEAD", "OPTIONS"]
      allowed_origins    = ["*"]
      exposed_headers    = ["*"]
      max_age_in_seconds = 3600
    }
  }

  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "azurerm_cdn_profile" "spa" {
  name                = "${var.project_name}-${var.environment}-cdn"
  location            = "global"
  resource_group_name = var.resource_group_name
  sku                 = "Standard_Microsoft"

  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "azurerm_cdn_endpoint" "spa" {
  name                = "${var.project_name}-${var.environment}-admin-web"
  profile_name        = azurerm_cdn_profile.spa.name
  location            = "global"
  resource_group_name = var.resource_group_name
  is_http_allowed     = false
  is_https_allowed    = true
  querystring_caching_behaviour = "IgnoreQueryString"

  origin {
    name      = "storage-origin"
    host_name = azurerm_storage_account.spa.primary_web_host
  }

  origin_host_header = azurerm_storage_account.spa.primary_web_host

  delivery_rule {
    name  = "SpaRouting"
    order = 1

    url_file_extension_condition {
      operator         = "LessThan"
      negate_condition = false
      transforms       = ["Lowercase"]
      match_values     = ["1"]
    }

    url_rewrite_action {
      source_pattern          = "/"
      destination             = "/index.html"
      preserve_unmatched_path = false
    }
  }

  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}
