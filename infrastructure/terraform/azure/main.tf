locals {
  customer_api_port = 3001
  admin_api_port    = 3002
  customer_web_port = 3000
}

module "networking" {
  source = "./modules/networking"

  location     = var.location
  environment  = var.environment
  project_name = var.project_name
  vpc_cidr     = "10.0.0.0/16"
}

module "acr" {
  source = "./modules/acr"

  location            = var.location
  environment         = var.environment
  project_name        = var.project_name
  resource_group_name = module.networking.resource_group_name
  sku                 = var.environment == "prod" ? "Standard" : "Basic"

  depends_on = [module.networking]
}

module "redis" {
  source = "./modules/redis"

  location            = var.location
  environment         = var.environment
  project_name        = var.project_name
  resource_group_name = module.networking.resource_group_name
  vnet_id             = module.networking.vnet_id
  redis_subnet_id     = module.networking.redis_subnet_id
  capacity            = var.redis_capacity
  family              = var.redis_family
  sku_name            = var.redis_sku

  depends_on = [module.networking]
}

module "key_vault" {
  source = "./modules/key-vault"

  location            = var.location
  environment         = var.environment
  project_name        = var.project_name
  resource_group_name = module.networking.resource_group_name

  secrets = {
    DATABASE_URL              = var.database_url
    MAILTRAP_API_KEY          = var.mailtrap_api_key
    STRIPE_SECRET_KEY         = var.stripe_secret_key
    STRIPE_WEBHOOK_SECRET     = var.stripe_webhook_secret
    TWO_FACTOR_ENCRYPTION_KEY = var.two_factor_encryption_key
    SMSPORTAL_CLIENT_ID       = var.smsportal_client_id
    SMSPORTAL_API_SECRET      = var.smsportal_api_secret
    REDIS_URL                 = module.redis.redis_url
  }

  depends_on = [module.networking, module.redis]
}

module "customer_api" {
  source = "./modules/container-apps"

  location                     = var.location
  environment                  = var.environment
  project_name                 = var.project_name
  resource_group_name          = module.networking.resource_group_name
  service_name                 = "customer-api"
  image                        = var.customer_api_image
  port                         = local.customer_api_port
  container_app_environment_id = module.networking.container_app_environment_id
  min_replicas                 = var.customer_api_min_replicas
  max_replicas                 = var.customer_api_max_replicas
  cpu                          = 0.5
  memory                       = "1Gi"
  key_vault_id                 = module.key_vault.vault_id

  env_vars = {
    NODE_ENV               = var.environment
    PORT                   = tostring(local.customer_api_port)
    CORS_ORIGIN            = var.customer_web_url
    STRIPE_PUBLISHABLE_KEY = var.stripe_publishable_key
    MAILTRAP_FROM          = var.mailtrap_from
    MAILTRAP_FROM_NAME     = var.mailtrap_from_name
  }

  secret_env_vars = {
    DATABASE_URL          = module.key_vault.secret_versionless_ids["DATABASE_URL"]
    MAILTRAP_API_KEY      = module.key_vault.secret_versionless_ids["MAILTRAP_API_KEY"]
    STRIPE_SECRET_KEY     = module.key_vault.secret_versionless_ids["STRIPE_SECRET_KEY"]
    STRIPE_WEBHOOK_SECRET = module.key_vault.secret_versionless_ids["STRIPE_WEBHOOK_SECRET"]
    SMSPORTAL_CLIENT_ID   = module.key_vault.secret_versionless_ids["SMSPORTAL_CLIENT_ID"]
    SMSPORTAL_API_SECRET  = module.key_vault.secret_versionless_ids["SMSPORTAL_API_SECRET"]
    REDIS_URL             = module.key_vault.secret_versionless_ids["REDIS_URL"]
  }

  depends_on = [module.networking, module.key_vault]
}

module "admin_api" {
  source = "./modules/container-apps"

  location                     = var.location
  environment                  = var.environment
  project_name                 = var.project_name
  resource_group_name          = module.networking.resource_group_name
  service_name                 = "admin-api"
  image                        = var.admin_api_image
  port                         = local.admin_api_port
  container_app_environment_id = module.networking.container_app_environment_id
  min_replicas                 = var.admin_api_min_replicas
  max_replicas                 = var.admin_api_max_replicas
  cpu                          = 0.5
  memory                       = "1Gi"
  key_vault_id                 = module.key_vault.vault_id

  env_vars = {
    NODE_ENV           = var.environment
    PORT               = tostring(local.admin_api_port)
    CORS_ORIGIN        = var.admin_web_url
    MAILTRAP_FROM      = var.mailtrap_from
    MAILTRAP_FROM_NAME = var.mailtrap_from_name
  }

  secret_env_vars = {
    DATABASE_URL              = module.key_vault.secret_versionless_ids["DATABASE_URL"]
    MAILTRAP_API_KEY          = module.key_vault.secret_versionless_ids["MAILTRAP_API_KEY"]
    TWO_FACTOR_ENCRYPTION_KEY = module.key_vault.secret_versionless_ids["TWO_FACTOR_ENCRYPTION_KEY"]
    SMSPORTAL_CLIENT_ID       = module.key_vault.secret_versionless_ids["SMSPORTAL_CLIENT_ID"]
    SMSPORTAL_API_SECRET      = module.key_vault.secret_versionless_ids["SMSPORTAL_API_SECRET"]
    REDIS_URL                 = module.key_vault.secret_versionless_ids["REDIS_URL"]
  }

  depends_on = [module.networking, module.key_vault]
}

module "customer_web" {
  source = "./modules/container-apps"

  location                     = var.location
  environment                  = var.environment
  project_name                 = var.project_name
  resource_group_name          = module.networking.resource_group_name
  service_name                 = "customer-web"
  image                        = var.customer_web_image
  port                         = local.customer_web_port
  container_app_environment_id = module.networking.container_app_environment_id
  min_replicas                 = var.customer_web_min_replicas
  max_replicas                 = var.customer_web_max_replicas
  cpu                          = 0.5
  memory                       = "1Gi"
  key_vault_id                 = module.key_vault.vault_id

  env_vars = {
    NODE_ENV                           = var.environment
    PORT                               = tostring(local.customer_web_port)
    NEXT_PUBLIC_API_URL                = module.customer_api.service_url
    NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY = var.stripe_publishable_key
  }

  secret_env_vars = {}

  depends_on = [module.networking, module.customer_api]
}

module "admin_web" {
  source = "./modules/static-site"

  location            = var.location
  environment         = var.environment
  project_name        = var.project_name
  resource_group_name = module.networking.resource_group_name
  domain              = var.admin_web_domain

  depends_on = [module.networking]
}
