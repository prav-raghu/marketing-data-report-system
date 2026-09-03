# DevOps Documentation for Project

This directory contains the necessary configurations and scripts for deploying and managing the application using Docker and Kubernetes.

## Directory Structure

- **docker/**: Contains Dockerfiles for building images for the API gateway, customer API, and admin API.
- **docker-compose.yml**: Defines the services and configurations for running the application locally using Docker Compose.
- **docker-compose.dev.yml**: Development-focused compose file with additional tooling (Adminer, MailHog, Redis Commander).
- **k8s/**: Contains Kubernetes deployment configurations for the API gateway, customer API, admin API, and Redis.
- **scripts/**: Includes various scripts for managing the development environment, running tests, and performing database migrations.

## Related Components

- **apps/automation/**: Workflow automation, now an ASP.NET Core (.NET 10) service hosting Elsa Workflows — see [automation README](../apps/automation/README.md). The n8n Docker/Kubernetes per-project-instance scaffold this replaced (documented below until Phase 9 of `documentation/dotnet-migration-plan.md`) no longer exists.

## Getting Started

To get started with the project, follow these steps:

1. **Clone the Repository**:
   ```
   git clone <repository-url>
   cd node-mono-repo-template
   ```

2. **Build Docker Images**:
   You can build the Docker images for the APIs using the provided Dockerfiles. For example, to build the API gateway:
   ```
   docker build -t node-mono-repo-template/api-gateway -f devops/docker/api-gateway.Dockerfile .
   ```

3. **Run with Docker Compose**:
   To run the entire application stack locally, use Docker Compose:
   ```
   docker-compose up
   ```

4. **Deploy to Kubernetes**:
   To deploy the application to a Kubernetes cluster, apply the deployment configurations:
   ```
   kubectl apply -f devops/k8s/
   ```

## Workflow Automation (Elsa Workflows)

`apps/automation` is now a single ASP.NET Core service (`WorkflowApi`), not a per-project-isolated set of containers the way n8n was — Elsa persists workflow definitions/instances to Postgres via `Elsa.Persistence.EFCore.PostgreSql` rather than needing a dedicated database/encryption key/storage volume per client. See [apps/automation/README.md](../apps/automation/README.md) for local development and environment variables.

## Scripts

- **dev.sh**: Starts the development environment.
- **test.sh**: Runs the tests for the application.

No standalone database migration script exists here anymore — `migrate.sh` and `migrate-deploy.sh` were Prisma-era scripts (`pnpm --filter @node-mono-repo-template/database prisma:migrate:deploy`) and were removed once `common/database` was ported to EF Core, since no EF Core equivalent (`dotnet ef database update`) has been wired up yet — no EF Core migrations exist in this repo yet. See the `migrate` service comment in root `docker-compose.yaml` and `ef-core.md` for the current state.

## Notes

- Ensure that you have Docker and Kubernetes set up on your machine before proceeding.
- Modify the `.env` files as necessary to configure environment variables for your local setup.

For further details on each component, refer to the respective README files in the individual API directories.
