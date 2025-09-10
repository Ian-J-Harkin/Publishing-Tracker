# Production Deployment Checklist

This document provides a checklist for the final steps of deploying the Publishing Tracker application to production.

## 1. Run the CI/CD Pipeline

-   [ ] Push all changes to the `main` branch.
-   [ ] Go to the **Actions** tab in your GitHub repository.
-   [ ] Select the **CI/CD Pipeline** workflow.
-   [ ] Click **Run workflow** and select the `main` branch.
-   [ ] Wait for the pipeline to complete successfully.

## 2. Run Database Migrations

-   [ ] Open the project in Visual Studio.
-   [ ] Download the publish profile from your backend App Service in the Azure portal.
-   [ ] Right-click the `PublishingTracker.Api` project and select **Publish**.
-   [ ] Import the publish profile.
-   [ ] In the **Service Dependencies** section, configure the database connection to use your Azure SQL Database.
-   [ ] In the **Entity Framework Migrations** section, check the box to apply migrations on publish.
-   [ ] Click **Publish**.

## 3. Final Testing

-   [ ] Open the production URL for your frontend App Service.
-   [ ] Test the user registration and login functionality.
-   [ ] Test all CRUD operations for books and sales.
-   [ ] Test the dashboard and platform request features.
-   [ ] Verify that the application is responsive on mobile devices.

Once all these steps are complete, the application will be successfully deployed to production.