---
description: Provision cloud infrastructure for the monorepo — generates Terraform modules for networking, database, cache, compute, and CDN
argument-hint: <cloud provider and requirements, e.g. "AWS with ECS Fargate, RDS Postgres, ElastiCache Redis, CloudFront for frontends">
---

Use the infrastructure subagent to provision cloud infrastructure for: $ARGUMENTS

1. Restructure `infrastructure/terraform/` into proper modules
2. Create `versions.tf` with provider version constraints
3. Create `variables.tf` with all inputs (project name, environment, region, instance sizes)
4. Create `outputs.tf` exposing connection strings, endpoints, and resource IDs
5. Create modules for each resource group (networking, database, cache, compute, storage, cdn, dns, secrets)
6. Create environment-specific variable files (dev.tfvars, staging.tfvars, prod.tfvars)
7. Create a remote state backend configuration
8. Create `terraform.tfvars.example` with placeholder values

Ensure all services (api-gateway, customer-api, admin-api, schedule-api, customer-web, admin-web) have the resources they need: managed PostgreSQL with private subnet access, managed Redis with encryption in transit, container orchestration for backend services, static/CDN hosting for frontend apps, secret management for JWT keys and DB credentials, load balancer with SSL termination.

Tag all resources with Project, Environment, ManagedBy.

Note: if this project is deploying to a self-hosted Hetzner VPS via Coolify rather than a managed cloud provider, use the deployment-coolify and vps-bootstrap subagents instead — this command is for cloud-managed infrastructure (AWS/Azure/GCP) only.
