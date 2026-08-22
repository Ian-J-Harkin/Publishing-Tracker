# CI/CD Pipeline Setup Guide

This guide provides step-by-step instructions for setting up and using the CI/CD pipeline to deploy the Publishing Tracker application to Azure.

## 1. Prerequisites

-   You have completed the steps in the `Azure_Deployment_Guide.md` to create the necessary Azure resources.
-   You have a GitHub repository for the project.

## 2. Configure GitHub Secrets

Azure credentials are only required for deployment workflows. The current repository workflows focus on CI validation.

1.  **Create a Service Principal:** In the Azure portal, open the Cloud Shell and run the following command, replacing `YOUR_SUBSCRIPTION_ID` and `YOUR_RESOURCE_GROUP` with your actual values:

    ```bash
    az ad sp create-for-rbac --name "PublishingTrackerDeploy" --role contributor --scopes /subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/YOUR_RESOURCE_GROUP --sdk-auth
    ```

2.  **Copy the JSON Output:** This command will output a JSON object with your service principal's credentials. Copy this entire object to your clipboard.

3.  **Create a GitHub Secret:**
    -   In your GitHub repository, go to **Settings > Secrets and variables > Actions**.
    -   Click **New repository secret**.
    -   For the name, enter `AZURE_CREDENTIALS`.
    -   For the value, paste the JSON object you copied from the Azure CLI.
    -   Click **Add secret**.

## 3. Using the CI/CD Pipelines

This project now uses split workflows:

-   `api.yml`: Builds and tests the .NET API (`apps/api/PublishingTracker.Api.sln`) when API files change.
-   `ui-react.yml`: Runs React checks when files in `apps/ui-react` change.
-   `ui-angular.yml`: Runs Angular checks when files in `apps/ui-angular` change.
-   `monorepo.yml`: Runs full monorepo validation and can be started manually with **Run workflow**.

### Trigger Model

1.  **Path-filtered runs:** On pushes/PRs, only workflows related to changed paths run automatically.
2.  **Full validation:** Use `monorepo.yml` from the **Actions** tab when you want an end-to-end check across all apps.

## 4. Post-Deployment Steps

After the first successful deployment, you will need to run the database migrations on the Azure SQL Database.

1.  **Get the Publish Profile:** In the Azure portal, navigate to your backend App Service (`PublishingTrackerApi`) and download the publish profile.
2.  **Run Migrations from Visual Studio:**
    -   Open `apps/api/PublishingTracker.Api.sln` in Visual Studio.
    -   Right-click the `PublishingTracker.Api` project (under `apps/api/src`) and select **Publish**.
    -   Import the publish profile you downloaded.
    -   In the **Service Dependencies** section, configure the database connection to use your Azure SQL Database.
    -   In the **Entity Framework Migrations** section, check the box to apply migrations on publish.
    -   Click **Publish**.

You have now successfully set up and used the CI/CD pipeline to deploy the application to Azure.