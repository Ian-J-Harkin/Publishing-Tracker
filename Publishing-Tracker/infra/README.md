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
- Azure SQL Server + database
- Azure Key Vault
- App Service plan + API Web App
- Key Vault RBAC assignment (API managed identity: Key Vault Secrets User)
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
- `SQL_ADMIN_PASSWORD_DEV`: SQL admin password for dev deployments
- `SQL_ADMIN_PASSWORD_PROD`: SQL admin password for prod deployments

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
     - `dev`: `SQL_ADMIN_PASSWORD_DEV`
     - `prod`: `SQL_ADMIN_PASSWORD_PROD`
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
2. Add `SQL_ADMIN_PASSWORD_DEV`.
3. Repeat in `prod` and add `SQL_ADMIN_PASSWORD_PROD`.

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

### Rotate SQL admin password

1. Generate a new strong password.
2. Update the SQL admin password in Azure SQL Server.
3. Update the matching GitHub environment secret:
   - `dev` -> `SQL_ADMIN_PASSWORD_DEV`
   - `prod` -> `SQL_ADMIN_PASSWORD_PROD`
4. Run `Infra IaC` manually with:
   - `environment=<target>`
   - `apply=true`
5. Validate app connectivity and API health checks.

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

- SQL enforces TLS 1.2 minimum.
- SQL firewall supports:
  - `sqlAllowAzureServices=true` (adds `0.0.0.0` rule),
  - optional `sqlAllowedClientIp`.
- Key Vault uses RBAC mode with purge protection enabled.
- API app uses managed identity and receives Key Vault secret-reader role at vault scope.
- HTTP 5xx metric alert is created for the API app.

If you later add private endpoints/VNet integration, set `sqlPublicNetworkAccess=Disabled` in the relevant environment parameter file.

## Notes

- This is a safe first scaffold. It does not yet include private networking, advanced RBAC, or alert rules.
- The API app setting `DefaultConnection` is set to a Key Vault reference. You must create the referenced secret in Key Vault (or extend templates to manage it).
