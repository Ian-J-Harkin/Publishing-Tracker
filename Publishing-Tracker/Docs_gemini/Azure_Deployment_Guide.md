# Azure Deployment Guide

This guide provides step-by-step instructions for configuring the necessary Azure resources to deploy the Publishing Tracker application.

## 1. Create a Resource Group

A resource group is a container that holds related resources for an Azure solution.

1.  **Sign in to the Azure portal.**
2.  In the search bar, type `Resource groups` and select it.
3.  Click **Create**.
4.  Select your subscription, give the resource group a name (e.g., `PublishingTrackerRG`), and choose a region.
5.  Click **Review + create**, then **Create**.

## 2. Create an Azure SQL Database

This will be the production database for the application.

1.  In the Azure portal, search for `SQL databases` and select it.
2.  Click **Create**.
3.  Select the resource group you just created.
4.  Enter a database name (e.g., `PublishingTrackerDB`).
5.  For the server, click **Create new**.
    -   Enter a unique server name, location, and admin credentials.
    -   Click **OK**.
6.  Choose the desired service tier (e.g., `Basic` for development).
7.  Click **Review + create**, then **Create**.
8.  Once the database is created, navigate to it and copy the **Connection string** (ADO.NET). You will need this later.

## 3. Create an Azure Key Vault

This will securely store the application's secrets.

1.  In the Azure portal, search for `Key Vault` and select it.
2.  Click **Create**.
3.  Select your resource group and give the Key Vault a unique name.
4.  Click **Review + create**, then **Create**.
5.  Once created, navigate to the Key Vault and go to **Secrets**.
6.  Click **Generate/Import**.
7.  Give the secret a name (e.g., `DefaultConnection`) and paste the database connection string as the value.
8.  Click **Create**.

## 4. Create Azure App Services

We will create two App Services: one for the backend API and one for the frontend React application.

### Backend App Service

1.  In the Azure portal, search for `App Services` and select it.
2.  Click **Create**.
3.  Select your resource group and give the App Service a unique name (e.g., `PublishingTrackerApi`).
4.  For the runtime stack, select `.NET 8`.
5.  Choose the desired App Service Plan.
6.  Click **Review + create**, then **Create**.

### Frontend App Service

1.  Follow the same steps as above, but give the App Service a different name (e.g., `PublishingTrackerUI`).
2.  For the runtime stack, select `Node.js 20 LTS`.

## 5. Configure App Service to Use Key Vault

1.  Navigate to the backend App Service (`PublishingTrackerApi`).
2.  Go to **Settings > Configuration**.
3.  Under **Application settings**, click **New application setting**.
4.  For the name, enter `DefaultConnection`.
5.  For the value, enter `@Microsoft.KeyVault(SecretUri=YOUR_SECRET_URI)`, replacing `YOUR_SECRET_URI` with the secret identifier from your Key Vault.
6.  Click **OK**, then **Save**.

You have now successfully configured all the necessary Azure resources for deployment. The next step is to set up the CI/CD pipeline.