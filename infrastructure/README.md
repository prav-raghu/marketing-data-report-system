# Infrastructure Directory

This directory contains infrastructure-related configurations and scripts for the project, including cloud provisioning (Terraform) and reverse proxy configurations (NGINX).

## Directory Structure

### NGINX (`nginx/`)

Production-ready NGINX reverse proxy configurations for the monorepo services.

**Features:**
- Load balancing across multiple service instances
- SSL/TLS termination with automatic certificate renewal
- Rate limiting and DDoS protection
- WebSocket support for real-time features
- Static file caching and compression (Gzip + Brotli)
- Security headers and HTTP/2
- Separate configurations for development and production

**Quick Start:**

```bash
# Development (Windows)
.\infrastructure\nginx\setup.ps1

# Development (Linux/Mac)
./infrastructure/nginx/setup.sh

# Or manually with Docker Compose
cd devops
docker-compose -f docker-compose.nginx.yml up -d
```

**Access Points (Development):**
- Customer App: http://localhost/
- Admin App: http://localhost/admin/
- API Gateway: http://localhost/api/

See [nginx/README.md](./nginx/README.md) for complete documentation.

### Terraform (`terraform/`)

The `terraform` folder includes the main configuration file for setting up the necessary infrastructure components required by the application. This includes resources such as databases, networking, and any other cloud services that are essential for the application's operation.

**Main Configuration:**

- **main.tf**: The primary Terraform configuration file where resources are defined and managed.

## Usage

To use the Terraform configurations, ensure that you have Terraform installed on your machine. You can initialize the Terraform workspace and apply the configurations using the following commands:

```bash
terraform init
terraform apply
```

Make sure to review the configurations and customize them according to your environment and requirements before applying.

## Notes

- Ensure that your cloud provider credentials are set up correctly in your environment.
- Review the Terraform documentation for more advanced usage and best practices.

This README serves as a guide for understanding and utilizing the infrastructure components of the project.