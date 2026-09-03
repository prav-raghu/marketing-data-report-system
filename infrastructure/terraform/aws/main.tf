locals {
  customer_api_port = 3001
  admin_api_port    = 3002
  customer_web_port = 3000
}

data "aws_caller_identity" "current" {}

resource "aws_ecs_cluster" "main" {
  name = "${var.project_name}-${var.environment}"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_ecs_cluster_capacity_providers" "main" {
  cluster_name       = aws_ecs_cluster.main.name
  capacity_providers = ["FARGATE", "FARGATE_SPOT"]

  default_capacity_provider_strategy {
    base              = 1
    weight            = 100
    capacity_provider = "FARGATE"
  }
}

module "networking" {
  source = "./modules/networking"

  region       = var.region
  environment  = var.environment
  project_name = var.project_name
  vpc_cidr     = "10.0.0.0/16"
}

module "ecr_customer_api" {
  source = "./modules/ecr"

  project_name = var.project_name
  environment  = var.environment
  service_name = "customer-api"
}

module "ecr_admin_api" {
  source = "./modules/ecr"

  project_name = var.project_name
  environment  = var.environment
  service_name = "admin-api"
}

module "ecr_customer_web" {
  source = "./modules/ecr"

  project_name = var.project_name
  environment  = var.environment
  service_name = "customer-web"
}

module "elasticache" {
  source = "./modules/elasticache"

  project_name            = var.project_name
  environment             = var.environment
  vpc_id                  = module.networking.vpc_id
  private_subnet_ids      = module.networking.private_subnet_ids
  redis_security_group_id = module.networking.redis_security_group_id
  node_type               = var.redis_node_type
  num_cache_nodes         = var.redis_num_cache_nodes

  depends_on = [module.networking]
}

module "secrets" {
  source = "./modules/secrets"

  project_name = var.project_name
  environment  = var.environment

  secrets = {
    DATABASE_URL              = var.database_url
    MAILTRAP_API_KEY          = var.mailtrap_api_key
    STRIPE_SECRET_KEY         = var.stripe_secret_key
    STRIPE_WEBHOOK_SECRET     = var.stripe_webhook_secret
    TWO_FACTOR_ENCRYPTION_KEY = var.two_factor_encryption_key
    SMSPORTAL_CLIENT_ID       = var.smsportal_client_id
    SMSPORTAL_API_SECRET      = var.smsportal_api_secret
    REDIS_URL                 = module.elasticache.redis_url
    REDIS_AUTH_TOKEN          = module.elasticache.auth_token
  }

  depends_on = [module.elasticache]
}

module "customer_api" {
  source = "./modules/ecs"

  project_name          = var.project_name
  environment           = var.environment
  region                = var.region
  service_name          = "customer-api"
  image                 = var.customer_api_image
  port                  = local.customer_api_port
  cpu                   = var.customer_api_cpu
  memory                = var.customer_api_memory
  min_capacity          = var.customer_api_min_capacity
  max_capacity          = var.customer_api_max_capacity
  cluster_id            = aws_ecs_cluster.main.id
  cluster_name          = aws_ecs_cluster.main.name
  vpc_id                = module.networking.vpc_id
  private_subnet_ids    = module.networking.private_subnet_ids
  public_subnet_ids     = module.networking.public_subnet_ids
  alb_security_group_id = module.networking.alb_security_group_id
  ecs_security_group_id = module.networking.ecs_security_group_id
  certificate_arn       = var.certificate_arn

  env_vars = {
    NODE_ENV               = var.environment
    PORT                   = tostring(local.customer_api_port)
    CORS_ORIGIN            = var.customer_web_url
    STRIPE_PUBLISHABLE_KEY = var.stripe_publishable_key
    MAILTRAP_FROM          = var.mailtrap_from
    MAILTRAP_FROM_NAME     = var.mailtrap_from_name
  }

  secret_env_vars = {
    DATABASE_URL          = module.secrets.secret_arns["DATABASE_URL"]
    MAILTRAP_API_KEY      = module.secrets.secret_arns["MAILTRAP_API_KEY"]
    STRIPE_SECRET_KEY     = module.secrets.secret_arns["STRIPE_SECRET_KEY"]
    STRIPE_WEBHOOK_SECRET = module.secrets.secret_arns["STRIPE_WEBHOOK_SECRET"]
    SMSPORTAL_CLIENT_ID   = module.secrets.secret_arns["SMSPORTAL_CLIENT_ID"]
    SMSPORTAL_API_SECRET  = module.secrets.secret_arns["SMSPORTAL_API_SECRET"]
    REDIS_URL             = module.secrets.secret_arns["REDIS_URL"]
    REDIS_AUTH_TOKEN      = module.secrets.secret_arns["REDIS_AUTH_TOKEN"]
  }

  depends_on = [module.networking, module.secrets, aws_ecs_cluster.main]
}

module "admin_api" {
  source = "./modules/ecs"

  project_name          = var.project_name
  environment           = var.environment
  region                = var.region
  service_name          = "admin-api"
  image                 = var.admin_api_image
  port                  = local.admin_api_port
  cpu                   = var.admin_api_cpu
  memory                = var.admin_api_memory
  min_capacity          = var.admin_api_min_capacity
  max_capacity          = var.admin_api_max_capacity
  cluster_id            = aws_ecs_cluster.main.id
  cluster_name          = aws_ecs_cluster.main.name
  vpc_id                = module.networking.vpc_id
  private_subnet_ids    = module.networking.private_subnet_ids
  public_subnet_ids     = module.networking.public_subnet_ids
  alb_security_group_id = module.networking.alb_security_group_id
  ecs_security_group_id = module.networking.ecs_security_group_id
  certificate_arn       = var.certificate_arn

  env_vars = {
    NODE_ENV           = var.environment
    PORT               = tostring(local.admin_api_port)
    CORS_ORIGIN        = var.admin_web_url
    MAILTRAP_FROM      = var.mailtrap_from
    MAILTRAP_FROM_NAME = var.mailtrap_from_name
  }

  secret_env_vars = {
    DATABASE_URL              = module.secrets.secret_arns["DATABASE_URL"]
    MAILTRAP_API_KEY          = module.secrets.secret_arns["MAILTRAP_API_KEY"]
    TWO_FACTOR_ENCRYPTION_KEY = module.secrets.secret_arns["TWO_FACTOR_ENCRYPTION_KEY"]
    SMSPORTAL_CLIENT_ID       = module.secrets.secret_arns["SMSPORTAL_CLIENT_ID"]
    SMSPORTAL_API_SECRET      = module.secrets.secret_arns["SMSPORTAL_API_SECRET"]
    REDIS_URL                 = module.secrets.secret_arns["REDIS_URL"]
    REDIS_AUTH_TOKEN          = module.secrets.secret_arns["REDIS_AUTH_TOKEN"]
  }

  depends_on = [module.networking, module.secrets, aws_ecs_cluster.main]
}

module "customer_web" {
  source = "./modules/ecs"

  project_name          = var.project_name
  environment           = var.environment
  region                = var.region
  service_name          = "customer-web"
  image                 = var.customer_web_image
  port                  = local.customer_web_port
  cpu                   = var.customer_web_cpu
  memory                = var.customer_web_memory
  min_capacity          = var.customer_web_min_capacity
  max_capacity          = var.customer_web_max_capacity
  cluster_id            = aws_ecs_cluster.main.id
  cluster_name          = aws_ecs_cluster.main.name
  vpc_id                = module.networking.vpc_id
  private_subnet_ids    = module.networking.private_subnet_ids
  public_subnet_ids     = module.networking.public_subnet_ids
  alb_security_group_id = module.networking.alb_security_group_id
  ecs_security_group_id = module.networking.ecs_security_group_id
  certificate_arn       = var.certificate_arn

  env_vars = {
    NODE_ENV                           = var.environment
    PORT                               = tostring(local.customer_web_port)
    NEXT_PUBLIC_API_URL                = module.customer_api.service_url
    NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY = var.stripe_publishable_key
  }

  secret_env_vars = {}

  depends_on = [module.networking, module.customer_api, aws_ecs_cluster.main]
}

module "admin_web" {
  source = "./modules/static-site"

  project_name    = var.project_name
  environment     = var.environment
  region          = var.region
  domain          = var.admin_web_domain
  certificate_arn = var.certificate_arn

  depends_on = [module.networking]
}
