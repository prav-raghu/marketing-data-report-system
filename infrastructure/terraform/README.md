# Terraform Infrastructure — Node Mono Repo Template

Modular Terraform configurations for deploying the platform to **AWS**, **GCP**, or **Azure**. All three mirror the same service set (`api-gateway`, `admin-api`, `customer-api`, `schedule-api`, `customer-web`, `admin-web`) defined in the root `docker-compose.yaml`, and follow the same module pattern.

> Note: Coolify on a VPS is the canonical deployment path for this template (see the `deployment-coolify` agent). This Terraform tree is the alternative managed-cloud path. Before any `terraform apply`, run the project-name substitution (the `init-project` skill) so no `node-mono-repo-template` placeholders, example domains, or stub registry/state names remain.

## Provider Comparison

| Component | GCP | AWS | Azure |
|---|---|---|---|
| **Backend services** | Cloud Run v2 | ECS Fargate | Container Apps |
| **Redis cache** | Memorystore | ElastiCache | Azure Cache for Redis |
| **Container registry** | Artifact Registry | ECR (per service) | ACR (shared) |
| **Admin SPA hosting** | Cloud Storage + Cloud CDN | S3 + CloudFront | Blob Storage + Azure CDN |
| **Secrets** | Secret Manager | Secrets Manager | Key Vault |
| **Networking** | VPC + Serverless VPC Connector | VPC + NAT Gateway | VNet + private endpoints |
| **State backend** | GCS bucket | S3 + DynamoDB | Azure Blob Storage |
| **Default region** | `africa-south1` (Johannesburg) | `af-south-1` (Cape Town) | `southafricanorth` (Johannesburg) |

---

## Directory Structure

```
infrastructure/terraform/
├── azure/
│   ├── providers.tf                 # AzureRM provider + Azure Blob state backend
│   ├── variables.tf                 # All input variables
│   ├── main.tf                      # Root module wiring
│   ├── outputs.tf                   # Service URLs + ACR login server
│   ├── terraform.tfvars.example     # Copy → terraform.tfvars and fill in secrets
│   ├── environments/
│   │   ├── dev.tfvars
│   │   ├── staging.tfvars
│   │   └── prod.tfvars
│   └── modules/
│       ├── networking/              # VNet, subnets, Container Apps Environment, Log Analytics
│       ├── container-apps/          # Reusable Azure Container App + KV role assignment
│       ├── redis/                   # Azure Cache for Redis with private endpoint
│       ├── acr/                     # Shared Azure Container Registry
│       ├── static-site/             # Blob Storage static site + Azure CDN
│       └── key-vault/               # Azure Key Vault secrets (RBAC-based)
├── gcp/
│   ├── providers.tf                 # GCP provider + GCS state backend
│   ├── variables.tf                 # All input variables
│   ├── main.tf                      # Root module wiring
│   ├── outputs.tf                   # Service URLs + registry URL
│   ├── terraform.tfvars.example     # Copy → terraform.tfvars and fill in secrets
│   ├── environments/
│   │   ├── dev.tfvars
│   │   ├── staging.tfvars
│   │   └── prod.tfvars
│   └── modules/
│       ├── networking/              # VPC, subnet, Serverless VPC Connector, NAT
│       ├── cloud-run/               # Reusable Cloud Run v2 service
│       ├── memorystore/             # Redis Memorystore (private VPC)
│       ├── artifact-registry/       # Docker image registry
│       ├── static-site/             # GCS bucket + Cloud CDN + HTTPS LB
│       └── secret-manager/         # Sensitive env vars in Secret Manager
└── aws/
    ├── providers.tf                 # AWS provider + S3/DynamoDB state backend
    ├── variables.tf                 # All input variables
    ├── main.tf                      # Root module wiring
    ├── outputs.tf                   # Service URLs + ECR URIs
    ├── terraform.tfvars.example     # Copy → terraform.tfvars and fill in secrets
    ├── environments/
    │   ├── dev.tfvars
    │   ├── staging.tfvars
    │   └── prod.tfvars
    └── modules/
        ├── networking/              # VPC, subnets, IGW, NAT, security groups
        ├── ecs/                     # Reusable ECS Fargate service + ALB
        ├── elasticache/             # Redis ElastiCache (private VPC)
        ├── ecr/                     # Container registry per service
        ├── static-site/             # S3 bucket + CloudFront + OAC
        └── secrets/                 # Sensitive env vars in Secrets Manager
```

---

## Azure Deployment

### Prerequisites

- Terraform >= 1.7.0
- Azure CLI authenticated (`az login`)
- An Azure subscription with Contributor access

### One-Time Setup

```bash
# Create resource group for state storage
az group create --name node-mono-repo-template-tfstate-rg --location southafricanorth

# Create storage account (name must be globally unique, 3-24 lowercase alphanumeric)
az storage account create \
  --name nmrttfstate \
  --resource-group node-mono-repo-template-tfstate-rg \
  --location southafricanorth \
  --sku Standard_LRS \
  --encryption-services blob \
  --min-tls-version TLS1_2

# Create the blob container for state
az storage container create \
  --name tfstate \
  --account-name nmrttfstate
```

### Deploy

```bash
cd infrastructure/terraform/azure

# Copy and fill in secrets
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — set database_url, API keys, etc.

terraform init
terraform plan -var-file=environments/prod.tfvars -var-file=terraform.tfvars
terraform apply -var-file=environments/prod.tfvars -var-file=terraform.tfvars
```

### Post-Deploy: Push Container Images

```bash
# Get ACR name from Terraform output
ACR_SERVER=$(terraform output -raw acr_login_server)

# Authenticate Docker with ACR
az acr login --name $ACR_SERVER

# Tag and push (repeat for admin-api, customer-web)
docker build -t $ACR_SERVER/customer-api:latest ./apps/backend/customer-api
docker push $ACR_SERVER/customer-api:latest
```

### Post-Deploy: Upload Admin Web SPA

```bash
# Build the SPA
pnpm --filter @node-mono-repo-template/admin-web build

# Get storage account name from Terraform output
STORAGE=$(terraform output -raw admin_web_storage_account)

# Upload to the $web container (Azure static website convention)
az storage blob upload-batch \
  --source ./apps/frontend/admin-web/dist \
  --destination '$web' \
  --account-name $STORAGE \
  --overwrite

# Purge Azure CDN cache
CDN_ENDPOINT_ID=$(terraform output -raw admin_web_cdn_endpoint_id)
az cdn endpoint purge \
  --ids $CDN_ENDPOINT_ID \
  --content-paths "/*"
```

### Azure Module Reference

| Module | Key Resources | Notes |
|---|---|---|
| `networking` | VNet, subnets, Container Apps Environment, Log Analytics | Environment shared across all Container Apps |
| `container-apps` | Azure Container App, system-assigned identity, KV role assignment | Reused for customer-api, admin-api, customer-web; scales to zero by default |
| `redis` | Azure Cache for Redis, private endpoint, private DNS zone | Private access only; TLS enforced (port 6380) |
| `acr` | Azure Container Registry | One shared registry per environment; admin user enabled |
| `static-site` | Blob Storage (static website), Azure CDN | SPA routing via CDN delivery rule rewrite |
| `key-vault` | Azure Key Vault (RBAC), Key Vault Secrets | Container Apps access via system-assigned managed identity |

---

## GCP Deployment

### Prerequisites

- Terraform >= 1.7.0
- `gcloud` CLI authenticated (`gcloud auth application-default login`)
- A GCP project with billing enabled

### One-Time Setup

```bash
# Create the remote state bucket (manually, before first terraform init)
gcloud storage buckets create gs://node-mono-repo-template-tfstate \
  --project=YOUR_PROJECT_ID \
  --location=africa-south1 \
  --uniform-bucket-level-access
```

### Deploy

```bash
cd infrastructure/terraform/gcp

# Copy and fill in secrets
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — set project_id, database_url, API keys, etc.

terraform init
terraform plan -var-file=environments/prod.tfvars -var-file=terraform.tfvars
terraform apply -var-file=environments/prod.tfvars -var-file=terraform.tfvars
```

### Post-Deploy: Push Container Images

```bash
# Authenticate Docker with Artifact Registry
gcloud auth configure-docker africa-south1-docker.pkg.dev

# Tag and push (repeat for admin-api, customer-web)
docker build -t africa-south1-docker.pkg.dev/YOUR_PROJECT/node-mono-repo-template-prod/customer-api:latest ./apps/backend/customer-api
docker push africa-south1-docker.pkg.dev/YOUR_PROJECT/node-mono-repo-template-prod/customer-api:latest
```

### Post-Deploy: Upload Admin Web SPA

```bash
# Build the SPA
pnpm --filter @node-mono-repo-template/admin-web build

# Sync to GCS bucket
gsutil -m rsync -r -d ./apps/frontend/admin-web/dist gs://BUCKET_NAME_FROM_OUTPUT
```

### GCP Module Reference

| Module | Key Resources | Notes |
|---|---|---|
| `networking` | VPC, subnet, Serverless VPC Connector, Cloud NAT | Connector enables Cloud Run → Redis private access |
| `cloud-run` | Cloud Run v2 service, IAM public access | Reused for customer-api, admin-api, customer-web |
| `memorystore` | Redis with TLS + AUTH | Sunday 02:00 maintenance window |
| `artifact-registry` | Docker repo + cleanup policies | Keeps 10 tagged images, purges untagged after 7 days |
| `static-site` | GCS bucket + Cloud CDN + HTTPS LB | SPA routing handled via 404→index.html |
| `secret-manager` | Secret Manager secrets | Cloud Run services IAM-bound to own secrets only |

---

## AWS Deployment

### Prerequisites

- Terraform >= 1.7.0
- AWS CLI authenticated (`aws configure` or IAM role)
- An AWS account with sufficient permissions

### One-Time Setup

```bash
# Create the S3 state bucket
aws s3api create-bucket \
  --bucket node-mono-repo-template-tfstate \
  --region af-south-1 \
  --create-bucket-configuration LocationConstraint=af-south-1

# Enable versioning and encryption
aws s3api put-bucket-versioning \
  --bucket node-mono-repo-template-tfstate \
  --versioning-configuration Status=Enabled

aws s3api put-bucket-encryption \
  --bucket node-mono-repo-template-tfstate \
  --server-side-encryption-configuration \
  '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'

# Create DynamoDB table for state locking
aws dynamodb create-table \
  --table-name node-mono-repo-template-tfstate-lock \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region af-south-1
```

### Deploy

```bash
cd infrastructure/terraform/aws

# Copy and fill in secrets
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — set database_url, API keys, etc.

terraform init
terraform plan -var-file=environments/prod.tfvars -var-file=terraform.tfvars
terraform apply -var-file=environments/prod.tfvars -var-file=terraform.tfvars
```

### Post-Deploy: Push Container Images

```bash
# Authenticate Docker with ECR
aws ecr get-login-password --region af-south-1 | \
  docker login --username AWS --password-stdin YOUR_ACCOUNT_ID.dkr.ecr.af-south-1.amazonaws.com

# Tag and push (repeat for admin-api, customer-web)
docker build -t YOUR_ACCOUNT_ID.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-prod-customer-api:latest ./apps/backend/customer-api
docker push YOUR_ACCOUNT_ID.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-prod-customer-api:latest
```

### Post-Deploy: Upload Admin Web SPA

```bash
# Build the SPA
pnpm --filter @node-mono-repo-template/admin-web build

# Sync to S3 bucket
aws s3 sync ./apps/frontend/admin-web/dist s3://BUCKET_NAME_FROM_OUTPUT --delete

# Invalidate CloudFront cache
aws cloudfront create-invalidation \
  --distribution-id DISTRIBUTION_ID_FROM_OUTPUT \
  --paths "/*"
```

### AWS Module Reference

| Module | Key Resources | Notes |
|---|---|---|
| `networking` | VPC, public/private subnets, IGW, NAT, 3x security groups | One NAT Gateway (single AZ for cost; use 2 for HA prod) |
| `ecs` | ECS Fargate service, ALB, auto-scaling | Reused for customer-api, admin-api, customer-web |
| `elasticache` | ElastiCache replication group (Redis 7.0) | TLS + AUTH enabled; `num_cache_nodes=2` enables replication |
| `ecr` | ECR repository + lifecycle policy | One repo per service; scan on push enabled |
| `static-site` | Private S3 + CloudFront OAC + distribution | SPA routing via 403/404 → index.html |
| `secrets` | Secrets Manager secrets | ECS execution role IAM-bound to own service secrets only |

---

## Environment Sizing Guide

| Tier | GCP | AWS Redis | AWS Fargate | Azure Redis | Azure Container Apps |
|---|---|---|---|---|---|
| **Dev** | min=0, BASIC Redis 1GB | `cache.t3.micro` × 1 | 256 CPU / 512 MB | Basic C1 | 0 min replicas |
| **Staging** | min=0, BASIC Redis 1GB | `cache.t3.micro` × 1 | 256 CPU / 512 MB | Standard C1 | 0 min replicas |
| **Prod** | min=1, STANDARD_HA Redis 2GB | `cache.r7g.large` × 2 | 512 CPU / 1024 MB | Premium P2 | 1 min replica |

---

## Security Notes

- All secrets are stored in the provider's managed secrets service — never in `.tfvars` files committed to git
- `terraform.tfvars` is gitignored; only `terraform.tfvars.example` is committed
- Redis is in private subnets with TLS and AUTH enabled in both providers
- ECS tasks and Cloud Run services use least-privilege IAM roles scoped to their own secrets
- S3 admin-web bucket blocks all public access; content served exclusively via CloudFront OAC

---

## Copying to a New Project

To reuse this infrastructure in a different project:

1. Copy the entire `infrastructure/terraform/` folder to your new repository
2. Update `project_name` variable default in `variables.tf` for GCP, AWS, and Azure
3. Update the state backend names in `providers.tf` for all three providers
4. Update domain names in `environments/*.tfvars`
5. Run one-time setup for remote state backends (all three providers)
6. Fill in secrets in `terraform.tfvars` (never commit this file)
