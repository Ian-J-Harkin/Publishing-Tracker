# User Testing Guide: Clean Architecture & CQRS Migration

This document outlines the testing scenarios required to verify the successful migration of the `PublishingTracker.Api` to a Clean Architecture layout using the CQRS (Command Query Responsibility Segregation) pattern with MediatR. 

The primary goal of these tests is to ensure **functional parity**. The API should behave exactly as it did before the migration, but the underlying execution now routes through the new application layers.

---

## 1. Platforms API Testing

**Objective:** Verify that `Platform` entities can be queried and created via MediatR handlers.

### Test 1.1: Retrieve Platforms
- **Action:** Send a `GET /api/platforms` request.
- **Expected Result:** Returns a 200 OK with a list of all platforms in the database.
- **Behind the Scenes:** Should trigger `GetPlatformsQuery` and be handled by `GetPlatformsQueryHandler`.

### Test 1.2: Create Platform
- **Action:** Send a `POST /api/platforms` request with a valid JSON body (Name, BaseUrl, CommissionRate).
- **Expected Result:** Returns a 200 OK (or 201 Created) with the newly created Platform ID.
- **Behind the Scenes:** Should trigger `CreatePlatformCommand`. The domain logic should enforce validation via the `Platform.Create()` factory method.

---

## 2. Sales API Testing

**Objective:** Verify that `Sale` records can be added, listed, and aggregated using the new CQRS handlers.

### Test 2.1: Retrieve Sales
- **Action:** Send a `GET /api/sales` request (optionally with `startDate` and `endDate` query parameters).
- **Expected Result:** Returns a 200 OK with the corresponding sales data.
- **Behind the Scenes:** Routes through `GetSalesQueryHandler`.

### Test 2.2: Retrieve Sales Summary
- **Action:** Send a `GET /api/sales/summary` request.
- **Expected Result:** Returns a 200 OK with aggregated sales metrics (Total Revenue, Total Royalties, Total Units Sold).
- **Behind the Scenes:** Routes through `GetSalesSummaryQueryHandler`.

### Test 2.3: Create Sale
- **Action:** Send a `POST /api/sales` request with a valid JSON body (BookId, PlatformId, SaleDate, Quantity, UnitPrice, Royalty, Currency, OrderId).
- **Expected Result:** Returns a 200 OK with the newly created Sale ID.
- **Behind the Scenes:** Should trigger `CreateSaleCommand` and enforce validation through the `Sale.Create()` factory method.

---

## 3. CSV Import & Background Processing Testing

**Objective:** Verify that the asynchronous CSV import process correctly delegates to MediatR and the infrastructure CSV reader service.

### Test 3.1: Enqueue Import Job
- **Action:** Send a `POST /api/import/csv` request (multipart/form-data) containing a valid sales CSV file and mapping data.
- **Expected Result:** 
  1. API immediately returns a `202 Accepted` indicating the job is queued.
  2. The database creates a new `ImportJob` record in "Pending" status.

### Test 3.2: Background Execution & Parsing
- **Action:** Wait for the background worker to process the queue.
- **Expected Result:** 
  1. The `ImportBackgroundService` picks up the job.
  2. It dispatches an `ImportCsvCommand` via MediatR.
  3. The `CsvReaderService` parses the file.
  4. Books and Platforms are automatically resolved or created.
  5. Duplicate sales (by OrderId) are skipped.
  6. The `ImportJob` status updates to "Completed".
- **Verification:** Check the database `ImportJobs`, `Books`, `Platforms`, and `Sales` tables to ensure records were appropriately mutated.

---

## 4. Dashboard API Testing

**Objective:** Verify that Dashboard endpoints correctly aggregate data using MediatR and EF Core.

### Test 4.1: Retrieve Dashboard Data
- **Action:** Send a `GET /api/dashboard/all` request.
- **Expected Result:** Returns a 200 OK with aggregated dashboard data (revenue by currency, YoY growth, seasonal trends, top platforms/books).
- **Behind the Scenes:** Should trigger `GetDashboardDataQuery` and be handled by `GetDashboardDataQueryHandler`.

### Test 4.2: Retrieve Dashboard Summary
- **Action:** Send a `GET /api/dashboard/summary` request.
- **Expected Result:** Returns a 200 OK with a lightweight summary (total books, total sales, revenue by currency).
- **Behind the Scenes:** Should trigger `GetDashboardSummaryQuery` and be handled by `GetDashboardSummaryQueryHandler`.

---

## 5. General Application Health

### Test 4.1: Database Migrations
- **Action:** Ensure the app boots and connects to the database successfully.
- **Expected Result:** EF Core should resolve the `PublishingTrackerDbContext` from its new location (`Infrastructure/Persistence`) without runtime dependency injection errors.

### Test 4.2: Authentication / Authorization
- **Action:** Attempt to access secure endpoints without a valid token.
- **Expected Result:** 401 Unauthorized.
- **Behind the Scenes:** The `TokenService` and `CurrentUserService` dependencies were moved to the Infrastructure layer, and should still be resolving correctly in the DI container.

---

## 6. Security & Admin Features Testing

**Objective:** Verify that the application correctly isolates tenant data and manages Administrator privileges according to the new Domain-Driven security model.

### Test 5.1: Admin Role Bootstrapping
- **Action:** Start with a fresh database and register two new users in sequence.
- **Expected Result:** The very first user registered is automatically assigned the `Admin` role. The second user is assigned the standard `User` role.

### Test 5.2: Role Promotion and Demotion
- **Action:** As the Admin user, navigate to the User Management dashboard and promote the second user to Admin.
- **Expected Result:** The second user is promoted. When the second user logs out and logs back in, they should see the Admin navigation links.
- **Action (Demotion):** As an Admin, demote the second user.
- **Expected Result:** The second user loses access. On their next login, they should no longer see the Admin UI.

### Test 5.3: Admin Lockout Prevention
- **Action:** As the sole remaining Admin user, attempt to demote yourself via the UI or API.
- **Expected Result:** The action is blocked. The domain layer throws an error, and the UI displays a failure message, ensuring the system is never left without an administrator.

### Test 5.4: Platform Approval Workflow
- **Action:** As a standard user, request/submit a new Platform integration.
- **Expected Result:** The platform is created but is immediately invisible to standard users (including the submitter) because it defaults to an inactive state.
- **Action:** As an Admin user, navigate to the Platform Management dashboard.
- **Expected Result:** The pending platform is visible. The Admin can approve it, at which point it becomes visible to all standard users.
