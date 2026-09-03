# Changesets

This folder contains changesets - markdown files that describe changes to packages.

## How to use

### Adding a changeset

When you make changes that need to be released, run:

```bash
pnpm changeset
```

This will prompt you to:

1. Select which packages have changed
2. Choose the type of change (major/minor/patch)
3. Write a summary of the changes

### Release workflow

1. **Development**: Create changesets as you work
2. **PR Review**: Changesets are reviewed with code changes
3. **Release**: CI creates a "Version Packages" PR
4. **Publish**: Merging the PR triggers publishing

### Versioning guidelines

- **Patch** (0.0.x): Bug fixes, dependency updates
- **Minor** (0.x.0): New features, non-breaking changes
- **Major** (x.0.0): Breaking changes

### Linked packages

Backend services are linked together - when one gets a minor/major bump, they all do:

- @node-mono-repo-template/api-gateway
- @node-mono-repo-template/customer-api
- @node-mono-repo-template/admin-api
- @node-mono-repo-template/schedule-api
