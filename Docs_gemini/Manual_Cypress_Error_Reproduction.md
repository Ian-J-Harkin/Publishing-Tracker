# Manual Reproduction of Cypress Login Error

## 1. Overview

This document outlines the steps to manually reproduce the login error that the Cypress end-to-end test is encountering. The core issue is a mismatch between the test user's credentials and the data in the running development database, leading to a failed login and a test timeout.

## 2. Prerequisites

-   A clean database (delete the `PublishingTracker.db` file if it exists).
-   The latest code changes, including the data seeding logic in `Program.cs`.

## 3. Steps to Reproduce

### Step 1: Start the Backend API

1.  Open a terminal in the root of the project.
2.  Run the following command to start the backend API:
    ```bash
    dotnet run --project src/PublishingTracker.Api/PublishingTracker.Api/
    ```
3.  **Observe the terminal output.** You should see logs indicating that the database is being created and the test user is being seeded.

### Step 2: Start the Frontend Application

1.  Open a **separate** terminal.
2.  Navigate to the `src/publishing-tracker-ui` directory.
3.  Run the following command to start the frontend development server:
    ```bash
    npm run dev
    ```

### Step 3: Manually Test the Login

1.  Open your web browser and navigate to `http://localhost:5173/login`.
2.  Open the browser's developer tools (usually by pressing `F12`) and switch to the "Console" and "Network" tabs to monitor for errors.
3.  In the login form, enter the following credentials:
    -   **Email:** `test@test.com`
    -   **Password:** `password123`
4.  Click the "Login" button.

## 4. Expected Result (Successful Test)

If the environment is set up correctly (i.e., the backend has started with the latest code and seeded the database), you should be successfully logged in and redirected to the dashboard page (`/dashboard`).

## 5. Error Condition (What Cypress is Experiencing)

If the backend API is running an older version of the code *without* the data seeding logic, the login will fail. You will observe the following:

-   The application will remain on the `/login` page.
-   The browser's developer console may show a `401 Unauthorized` or `500 Internal Server Error` for the `/api/auth/login` request.
-   The backend terminal will display a `BCrypt.Net.SaltParseException`.

This is the exact scenario that the Cypress test is encountering, causing it to time out while waiting for the URL to change to `/dashboard`.