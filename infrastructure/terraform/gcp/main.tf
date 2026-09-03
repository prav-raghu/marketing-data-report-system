locals {
  common_labels = {
    project     = var.project_name
    environment = var.environment
    managed-by  = "terraform"
  }

  customer_api_port = 3001
  admin_api_port    = 3002
  customer_web_port = 3000
}

resource "google_project_service" "required_apis" {
  for_each = toset([
    "run.googleapis.com",
    "redis.googleapis.com",
    "artifactregistry.googleapis.com",
    "secretmanager.googleapis.com",
    "vpcaccess.googleapis.com",
    "compute.googleapis.com",
    "iam.googleapis.com",
    "cloudresourcemanager.googleapis.com",
  ])

  project            = var.project_id
  service            = each.value
  disable_on_destroy = false
}

module "networking" {
  source = "./modules/networking"

  project_id   = var.project_id
  region       = var.region
  environment  = var.environment
  project_name = var.project_name

  depends_on = [google_project_service.required_apis]
}

module "artifact_registry" {
  source = "./modules/artifact-registry"

  project_id   = var.project_id
  region       = var.region
  environment  = var.environment
  project_name = var.project_name

  depends_on = [google_project_service.required_apis]
}

module "memorystore" {
  source = "./modules/memorystore"

  project_id   = var.project_id
  region       = var.region
  environment  = var.environment
  project_name = var.project_name
  memory_size  = var.redis_memory_size_gb
  tier         = var.redis_tier
  vpc_name     = module.networking.vpc_name

  depends_on = [module.networking]
}

module "secrets" {
  source = "./modules/secret-manager"

  project_id   = var.project_id
  environment  = var.environment
  project_name = var.project_name

  secrets = {
    DATABASE_URL              = var.database_url
    MAILTRAP_API_KEY          = var.mailtrap_api_key
    STRIPE_SECRET_KEY         = var.stripe_secret_key
    STRIPE_WEBHOOK_SECRET     = var.stripe_webhook_secret
    TWO_FACTOR_ENCRYPTION_KEY = var.two_factor_encryption_key
    SMSPORTAL_CLIENT_ID       = var.smsportal_client_id
    SMSPORTAL_API_SECRET      = var.smsportal_api_secret
    REDIS_URL                 = module.memorystore.redis_url
    REDIS_AUTH_STRING         = module.memorystore.auth_string
  }

  depends_on = [google_project_service.required_apis, module.memorystore]
}

resource "google_service_account" "customer_api" {
  account_id   = "${var.project_name}-${var.environment}-cust-api"
  display_name = "${var.project_name} customer-api (${var.environment})"
  project      = var.project_id
}

resource "google_service_account" "admin_api" {
  account_id   = "${var.project_name}-${var.environment}-admin-api"
  display_name = "${var.project_name} admin-api (${var.environment})"
  project      = var.project_id
}

resource "google_service_account" "customer_web" {
  account_id   = "${var.project_name}-${var.environment}-cust-web"
  display_name = "${var.project_name} customer-web (${var.environment})"
  project      = var.project_id
}

resource "google_secret_manager_secret_iam_member" "customer_api_secrets" {
  for_each = toset(["DATABASE_URL", "MAILTRAP_API_KEY", "STRIPE_SECRET_KEY", "STRIPE_WEBHOOK_SECRET", "SMSPORTAL_CLIENT_ID", "SMSPORTAL_API_SECRET", "REDIS_URL", "REDIS_AUTH_STRING"])

  project   = var.project_id
  secret_id = module.secrets.secret_ids[each.key]
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.customer_api.email}"
}

resource "google_secret_manager_secret_iam_member" "admin_api_secrets" {
  for_each = toset(["DATABASE_URL", "MAILTRAP_API_KEY", "TWO_FACTOR_ENCRYPTION_KEY", "SMSPORTAL_CLIENT_ID", "SMSPORTAL_API_SECRET", "REDIS_URL", "REDIS_AUTH_STRING"])

  project   = var.project_id
  secret_id = module.secrets.secret_ids[each.key]
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.admin_api.email}"
}

module "customer_api" {
  source = "./modules/cloud-run"

  project_id              = var.project_id
  region                  = var.region
  service_name            = "${var.project_name}-${var.environment}-customer-api"
  image                   = var.customer_api_image
  port                    = local.customer_api_port
  min_instances           = var.customer_api_min_instances
  max_instances           = var.customer_api_max_instances
  service_account_email   = google_service_account.customer_api.email
  serverless_connector_id = module.networking.serverless_connector_id
  allow_public_access     = true

  env_vars = {
    NODE_ENV               = var.environment
    PORT                   = tostring(local.customer_api_port)
    CORS_ORIGIN            = var.customer_web_url
    STRIPE_PUBLISHABLE_KEY = var.stripe_publishable_key
    MAILTRAP_FROM          = var.mailtrap_from
    MAILTRAP_FROM_NAME     = var.mailtrap_from_name
  }

  secret_env_vars = {
    DATABASE_URL          = module.secrets.secret_ids["DATABASE_URL"]
    MAILTRAP_API_KEY      = module.secrets.secret_ids["MAILTRAP_API_KEY"]
    STRIPE_SECRET_KEY     = module.secrets.secret_ids["STRIPE_SECRET_KEY"]
    STRIPE_WEBHOOK_SECRET = module.secrets.secret_ids["STRIPE_WEBHOOK_SECRET"]
    SMSPORTAL_CLIENT_ID   = module.secrets.secret_ids["SMSPORTAL_CLIENT_ID"]
    SMSPORTAL_API_SECRET  = module.secrets.secret_ids["SMSPORTAL_API_SECRET"]
    REDIS_URL             = module.secrets.secret_ids["REDIS_URL"]
    REDIS_AUTH_STRING     = module.secrets.secret_ids["REDIS_AUTH_STRING"]
  }

  depends_on = [module.networking, module.secrets]
}

module "admin_api" {
  source = "./modules/cloud-run"

  project_id              = var.project_id
  region                  = var.region
  service_name            = "${var.project_name}-${var.environment}-admin-api"
  image                   = var.admin_api_image
  port                    = local.admin_api_port
  min_instances           = var.admin_api_min_instances
  max_instances           = var.admin_api_max_instances
  service_account_email   = google_service_account.admin_api.email
  serverless_connector_id = module.networking.serverless_connector_id
  allow_public_access     = true

  env_vars = {
    NODE_ENV           = var.environment
    PORT               = tostring(local.admin_api_port)
    CORS_ORIGIN        = var.admin_web_url
    MAILTRAP_FROM      = var.mailtrap_from
    MAILTRAP_FROM_NAME = var.mailtrap_from_name
  }

  secret_env_vars = {
    DATABASE_URL              = module.secrets.secret_ids["DATABASE_URL"]
    MAILTRAP_API_KEY          = module.secrets.secret_ids["MAILTRAP_API_KEY"]
    TWO_FACTOR_ENCRYPTION_KEY = module.secrets.secret_ids["TWO_FACTOR_ENCRYPTION_KEY"]
    SMSPORTAL_CLIENT_ID       = module.secrets.secret_ids["SMSPORTAL_CLIENT_ID"]
    SMSPORTAL_API_SECRET      = module.secrets.secret_ids["SMSPORTAL_API_SECRET"]
    REDIS_URL                 = module.secrets.secret_ids["REDIS_URL"]
    REDIS_AUTH_STRING         = module.secrets.secret_ids["REDIS_AUTH_STRING"]
  }

  depends_on = [module.networking, module.secrets]
}

module "customer_web" {
  source = "./modules/cloud-run"

  project_id              = var.project_id
  region                  = var.region
  service_name            = "${var.project_name}-${var.environment}-customer-web"
  image                   = var.customer_web_image
  port                    = local.customer_web_port
  min_instances           = var.customer_web_min_instances
  max_instances           = var.customer_web_max_instances
  service_account_email   = google_service_account.customer_web.email
  serverless_connector_id = module.networking.serverless_connector_id
  allow_public_access     = true

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

  project_id   = var.project_id
  region       = var.region
  environment  = var.environment
  project_name = var.project_name
  domain       = var.admin_web_domain

  depends_on = [google_project_service.required_apis]
}
