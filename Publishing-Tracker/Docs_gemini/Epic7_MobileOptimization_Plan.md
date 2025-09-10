# Epic 7: Mobile Optimization & Deployment - Detailed Implementation Plan

## 1. Frontend Development (React)

### Milestone 1: Mobile Responsiveness

- **Task 1.1: Optimize Dashboard for Mobile**
  - In `DashboardPage.tsx`, use a responsive grid system (e.g., CSS Grid or Flexbox with media queries) to stack metric cards vertically on smaller screens.
  - In the chart components, ensure the `recharts` library is configured with `responsiveContainer` to allow charts to resize gracefully.
  - Implement a collapsible navigation menu for mobile, likely by modifying the main `App.tsx` layout to include a hamburger menu that toggles a sidebar.

- **Task 1.2: Optimize Forms for Mobile**
  - In `AddBookPage.tsx`, `EditBookPage.tsx`, `AddSalePage.tsx`, and `RequestPlatformPage.tsx`, apply responsive styles to ensure form inputs and labels are legible and easy to use on mobile.
  - For numeric inputs, set the `inputMode` attribute to "numeric" to bring up the numeric keypad on mobile devices.
  - Implement an auto-save feature using `localStorage` and a `useEffect` hook with a debounce function to prevent data loss on mobile connections.

## 2. Testing and Quality Assurance

### Milestone 2: Comprehensive Testing

- **Task 2.1: End-to-End (E2E) Testing on Mobile**
  - Create a new Cypress test file, `mobile_experience.cy.ts`.
  - Use `cy.viewport()` to set the screen size to a mobile resolution (e.g., `iphone-6`).
  - Write tests that log in, navigate to the dashboard, and verify that the layout has adapted for mobile.
  - Test form submissions on the mobile viewport to ensure they are still functional.

- **Task 2.2: Cross-Browser and Cross-Device Testing**
  - Manually test the application on Chrome, Firefox, and Safari.
  - Use browser developer tools to emulate various mobile devices (e.g., iPhone, iPad, Android devices).
  - If possible, test on at least one physical mobile device to check for touch-specific issues.

## 3. Deployment

### Milestone 3: Azure Deployment

- **Task 3.1: Configure Azure Resources**
  - In the Azure portal, create a new App Service Plan and an App Service for the frontend and backend.
  - Create a new Azure SQL Database and configure the connection string.
  - Create a new Azure Key Vault to store secrets like the database connection string and JWT secret.

- **Task 3.2: Set Up CI/CD Pipeline**
  - In your GitHub repository, create a new GitHub Actions workflow file.
  - The workflow should have steps to build the .NET API and the React frontend.
  - Add steps to deploy the build artifacts to the Azure App Services.
  - Configure the workflow to run automatically on pushes to the `main` branch.

- **Task 3.3: Deploy to Production**
  - Run the GitHub Actions workflow to deploy the application.
  - In the Azure portal, configure the App Service to use the secrets from the Key Vault.
  - Run the EF Core database migrations on the Azure SQL Database.
  - Perform a final round of testing on the production URL to ensure everything is working as expected.