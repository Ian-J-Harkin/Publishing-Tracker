# Monorepo Structure and DevOps Plan

This note captures the recommended target layout for the Publishing Tracker repo and the DevOps changes needed to support it.

## Current shape

- `apps/api` contains the .NET solution and tests
- `apps/ui-react` contains the React app
- `apps/ui-angular` contains the Angular app
- root `package.json` orchestrates the frontend workspaces
- root shell scripts handle manual startup and CI-like checks

The repo is already a monorepo in practice, but the structure is still uneven:

- the API tree is nested and noisy
- stale artifact folders and checked-in logs are still present
- DevOps is mostly script-driven rather than workflow-driven

## Recommended target layout

```text
Publishing-Tracker/
  apps/
    api/
      src/
        PublishingTracker.Api/
        PublishingTracker.Application/
        PublishingTracker.Domain/
        PublishingTracker.Infrastructure/
      tests/
        PublishingTracker.Api.Tests/
      PublishingTracker.Api.sln

    ui-react/
      publishing-tracker-ui/

    ui-angular/
      publishing-tracker-ui-angular/

  packages/
    shared-types/   # only if genuinely shared

  eng/
    scripts/
    ci/

  .github/
    workflows/
      api.yml
      ui-react.yml
      ui-angular.yml
      monorepo.yml

  Directory.Build.props
  Directory.Build.targets
  Directory.Packages.props
  global.json
  package.json
```

## Migration plan

### 1. Clean up the tree

- delete checked-in logs and investigation files
- remove empty placeholder folders
- delete old feature-folder artifacts

### 2. Flatten the API solution

- move the .NET projects into `apps/api/src`
- move tests into `apps/api/tests`
- keep the solution in the API app root

### 3. Move DTO source files to the application-side contract home

- keep the `Application.Common.Dtos` namespace
- move the files physically out of `Domain/Dtos`

### 4. Standardize .NET build configuration

- add `Directory.Build.props`
- add `Directory.Build.targets`
- add `Directory.Packages.props`
- add `global.json`

### 5. Formalize frontend orchestration

- keep root `package.json` as the workspace orchestrator
- add scripts like `build:api`, `build:web-react`, `build:web-angular`
- add matching test/lint scripts

### 6. Add real CI workflows

Recommended workflow split:

- `api.yml`
- `ui-react.yml`
- `ui-angular.yml`
- optional `monorepo.yml` for umbrella PR runs

Use path filters so each workflow runs only when its app changes.

### 7. Keep deployment independent

- API deploys separately from each frontend
- cache NuGet and npm dependencies
- publish artifacts per app

## Guidance

- Prefer small, explicit shared packages only when code is genuinely shared.
- Do not introduce a shared package just to avoid folder duplication.
- Keep the monorepo simple: one repo, three apps, clear boundaries.

## Related todo

- `document-monorepo-target-structure`

