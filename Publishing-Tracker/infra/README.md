# Infrastructure as Code (IaC)

This folder contains Azure infrastructure templates for Publishing Tracker.

## Layout

- `bicep/main.bicep`: Subscription-scope entrypoint template
- `bicep/modules/`: Reusable resource modules
- `env/dev.parameters.json`: Development environment parameters
- `env/prod.parameters.json`: Production environment parameters

## What this scaffold provisions

- Resource group
- Log Analytics workspace
- Application Insights
- Azure Key Vault (storing `NeonConnection` secret for external Neon Serverless PostgreSQL + `pgvector`, plus references for `JwtKey` and `AIProviderApiKey`)
- App Service plan + API Web App (`apiWebApp`) + Agent Web App (`agentWebApp`)
- Key Vault RBAC assignments (`apiWebApp` and `agentWebApp` managed identities: Key Vault Secrets User)
- Baseline monitoring alert for API HTTP 5xx

## GitHub workflow

The IaC workflow is defined in:

- `.github/workflows/infra.yml`

It runs:

- Bicep validation (`az bicep build`)
- PR `what-if` preview (dev parameters)
- Manual deployment for `dev` or `prod`

## Required GitHub secrets

- `AZURE_CREDENTIALS`: Service principal JSON for `azure/login`
- `NEON_CONNECTION_STRING_DEV`: Neon Serverless PostgreSQL (`pgvector`) connection string for dev deployments
- `NEON_CONNECTION_STRING_PROD`: Neon Serverless PostgreSQL (`pgvector`) connection string for prod deployments

Optional (set in parameter files):

- `alertEmailReceiver`: Email address for alert notifications

## GitHub setup checklist

1. Create GitHub Environments:
   - `dev`
   - `prod`
2. Configure environment protections:
   - Require manual approval for `prod` deployments.
   - Optionally restrict `prod` to `main` branch.
3. Add required secrets:
   - Repository-level: `AZURE_CREDENTIALS`
   - Environment-level (recommended):
     - `dev`: `NEON_CONNECTION_STRING_DEV`
     - `prod`: `NEON_CONNECTION_STRING_PROD`
4. Validate workflow behavior:
   - Open a PR touching `infra/**` to confirm `what-if` runs.
   - Run `Infra IaC` manually with `environment=dev`, `apply=false` to validate guards.
   - Run `Infra IaC` manually with `environment=dev|prod`, `apply=true` to deploy.

The workflow fails fast with explicit errors if required secrets are missing.

## GitHub UI runbook (exact click-path)

### 1) Create environments

1. Open repository in GitHub.
2. Go to **Settings** -> **Environments**.
3. Click **New environment**.
4. Create `dev`.
5. Repeat and create `prod`.

### 2) Configure protection rules

For `prod`:

1. **Settings** -> **Environments** -> **prod**.
2. In **Deployment protection rules**, enable **Required reviewers**.
3. Add one or more approvers.
4. (Optional) In **Deployment branches and tags**, restrict to `main`.

For `dev`:

1. **Settings** -> **Environments** -> **dev**.
2. Leave open for faster iteration, or add reviewers if desired.

### 3) Add secrets

Repository secret:

1. **Settings** -> **Secrets and variables** -> **Actions**.
2. Under **Repository secrets**, click **New repository secret**.
3. Name: `AZURE_CREDENTIALS`.
4. Value: Azure service principal JSON.

Environment secrets:

1. **Settings** -> **Environments** -> **dev** -> **Secrets and variables** -> **Actions**.
2. Add `NEON_CONNECTION_STRING_DEV`.
3. Repeat in `prod` and add `NEON_CONNECTION_STRING_PROD`.

### 4) Validate approval flow

1. Open a PR that changes files under `Publishing-Tracker/infra/**`.
2. Confirm `Infra IaC` runs validation and `what-if`.
3. Go to **Actions** -> **Infra IaC** -> **Run workflow**.
4. Select:
   - `environment=prod`
   - `apply=true`
5. Confirm job pauses at environment protection.
6. Approver opens the run and clicks **Review deployments** -> **Approve and deploy**.

## Day-2 operations runbook

### Rotate Neon database credentials

1. Rotate the PostgreSQL role password in the Neon Console.
2. Update the Neon connection string in the matching GitHub environment secret:
   - `dev` -> `NEON_CONNECTION_STRING_DEV`
   - `prod` -> `NEON_CONNECTION_STRING_PROD`
3. Run `Infra IaC` manually with:
   - `environment=<target>`
   - `apply=true` (updates the `NeonConnection` secret in Azure Key Vault).
4. Ensure `JwtKey` and `AIProviderApiKey` secrets exist in Key Vault, then validate app connectivity and `/health/ready` checks on both `apiWebApp` and `agentWebApp`.

### Rollback approach

1. Open **Actions** and locate the last successful `Infra IaC` deployment run for the target environment.
2. Re-run deployment with the last known-good commit/branch state (or revert the infra commit and deploy again).
3. Run deployment with `apply=true`.
4. Confirm resource state and API health.

Note: This workflow does not yet implement automatic rollback. Rollback is currently a controlled redeploy to known-good IaC.

### Emergency deployment freeze / unfreeze

Freeze:

1. Go to **Settings** -> **Environments** -> `prod`.
2. Enable strict protection (required reviewers and restricted branches).
3. Remove/limit approvers temporarily so no deployment can be approved.

Unfreeze:

1. Restore normal approver access in **Settings** -> **Environments** -> `prod`.
2. Trigger a controlled deployment using `Infra IaC` (`environment=prod`, `apply=true`).
3. Confirm deployment completion and health checks.

## Security defaults in this scaffold

- Neon PostgreSQL connections require TLS (`Ssl Mode=Require`).
- Both API and Agent enforce HTTPS (`httpsOnly: true`, `minTlsVersion: '1.2'`) and connect to `AIProvider__McpEndpoint` over HTTPS (`https://<apiHostName>/mcp`).
- Key Vault uses RBAC mode with purge protection enabled.
- Both `apiWebApp` and `agentWebApp` use SystemAssigned managed identities and receive Key Vault Secrets User role at vault scope.
- HTTP 5xx metric alert is created for the API app.

## Notes

- Neon Serverless PostgreSQL projects/branches (with `pgvector` enabled) are provisioned externally in Neon and injected into Azure Key Vault via `neonConnectionString` (`NeonConnection` secret).
- App settings for `Jwt__Key` and `AIProvider__ApiKey` use Key Vault references (`secrets/JwtKey/` and `secrets/AIProviderApiKey/`). Provision those two secrets in Key Vault alongside `NeonConnection` before starting production traffic.
