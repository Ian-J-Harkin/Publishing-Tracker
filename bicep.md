# Azure Deployment Guide

## 1. Create a Resource Group
- Sign in to [Azure Portal](https://portal.azure.com)
- Search for **Resource groups**
- Click **Create**
- Choose subscription, name (e.g., `PublishingTrackerRG`), and region
- Click **Review + create**, then **Create**

## 2. Create Azure SQL Database
- Search for **SQL databases**
- Click **Create**
- Select resource group
- Name: `PublishingTrackerDB`
- Server: **Create new**
  - Name: `publishingtracker`
  - Region: `West Europe`
  - Auth: Microsoft Entra-only
- Choose tier: `Basic`
- Click **Review + create**, then **Create**
- Copy ADO.NET connection string

## 3. Create Azure Key Vault
- Search for **Key Vault**
- Click **Create**
- Name: `PublishingTrackerVault`
- Add secret: `DefaultConnection` with connection string

## 4. Create App Services
### Backend
- Name: `PublishingTrackerApi`
- Runtime: `.NET 8`
- Plan: Basic

### Frontend
- Name: `PublishingTrackerUI`
- Runtime: `Node.js 20 LTS`

## 5. Configure Key Vault Reference
- Go to App Service > Configuration
- Add setting:
  - Name: `DefaultConnection`
  - Value:  
    ```
    @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/DefaultConnection/<version>)
    ```

## Optional: Enable Managed Identity
- App Service > Identity > Enable system-assigned
- Key Vault > Access policies > Add Secret Reader role
