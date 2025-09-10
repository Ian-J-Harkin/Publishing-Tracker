# CI/CD Pipeline Setup Guide

This guide provides step-by-step instructions for setting up and using the CI/CD pipeline to deploy the Publishing Tracker application to Azure.

## 1. Prerequisites

-   You have completed the steps in the `Azure_Deployment_Guide.md` to create the necessary Azure resources.
-   You have a GitHub repository for the project.

## 2. Configure GitHub Secrets

The CI/CD pipeline requires a secret to authenticate with Azure.

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

This project includes two CI/CD pipelines:

-   `dotnet.yml`: A basic pipeline that builds and tests the .NET backend. This is useful for quick validation of backend changes.
-   `cicd.yml`: A comprehensive pipeline that builds, tests, and deploys both the backend and frontend. This is the primary pipeline for production deployments.

### Running the `cicd.yml` Pipeline

1.  **Push to `main`:** The pipeline is configured to run automatically on pushes to the `main` branch. However, the deployment jobs will only run when manually triggered.

2.  **Manual Deployment:**
    -   In your GitHub repository, go to the **Actions** tab.
    -   Select the **CI/CD Pipeline** workflow.
    -   Click **Run workflow** and select the `main` branch.
    -   This will trigger the build, test, and deployment jobs.

## 4. Post-Deployment Steps

After the first successful deployment, you will need to run the database migrations on the Azure SQL Database.

1.  **Get the Publish Profile:** In the Azure portal, navigate to your backend App Service (`PublishingTrackerApi`) and download the publish profile.
2.  **Run Migrations from Visual Studio:**
    -   Open the project in Visual Studio.
    -   Right-click the `PublishingTracker.Api` project and select **Publish**.
    -   Import the publish profile you downloaded.
    -   In the **Service Dependencies** section, configure the database connection to use your Azure SQL Database.
    -   In the **Entity Framework Migrations** section, check the box to apply migrations on publish.
    -   Click **Publish**.

You have now successfully set up and used the CI/CD pipeline to deploy the application to Azure.