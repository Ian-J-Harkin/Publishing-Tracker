# Epic 3: Sales Data Management - Implementation Plan

## 1. Backend Development (.NET API)

### Milestone 1: Core API Setup

- **Task 1.1: Create Sale Entity and DTOs**
  - Define the `Sale` entity in `PublishingTracker.Api/Models/` with all required properties as specified in the database schema.
  - Create `SaleDto` and `CreateSaleDto` in `PublishingTracker.Api/Models/Dtos/` to handle data transfer.

- **Task 1.2: Update Database Context**
  - Add a `DbSet<Sale>` property to the `PublishingTrackerDbContext`.
  - Configure the relationships between `Sale`, `Book`, `User`, and `Platform` entities.

- **Task 1.3: Database Migration**
  - Generate a new EF Core migration to add the `Sales` table to the database.
  - Apply the migration to update the database schema.

### Milestone 2: API Endpoint Implementation

- **Task 2.1: Map Sales API Endpoints**
  - In `Program.cs`, create a new route group for `/api/sales`.
  - Implement dependency injection for the `PublishingTrackerDbContext`.

- **Task 2.2: Implement CRUD Endpoint Handlers**
  - **`GET /api/sales`**: Retrieve a list of all sales for the authenticated user, with options for filtering and sorting.
  - **`POST /api/sales`**: Create a new manual sale entry.

- **Task 2.3: Add Validation and Business Logic**
  - Implement validation for the `CreateSaleDto`.
  - Add logic to automatically calculate the `Revenue` field based on `Quantity` and `Royalty`.

## 2. Frontend Development (React)

### Milestone 3: UI Component Development

- **Task 3.1: Create `saleService.ts`**
  - Create a new service in `publishing-tracker-ui/src/services/` to handle API calls to the sales endpoints.

- **Task 3.2: Develop Sales Page Component**
  - Create a `SalesPage` component in `publishing-tracker-ui/src/pages/` to display the sales overview and detailed sales list.
  - Implement functionality to fetch and display sales data.
  - Add UI elements for filtering by date, book, and platform.

- **Task 3.3: Develop Add Sale Form**
  - Create an `AddSalePage` component with a form for manual sale entry.
  - The form should include dropdowns for selecting a book and a platform.

## 3. Testing and Quality Assurance

### Milestone 4: Comprehensive Testing

- **Task 4.1: Backend Integration Tests**
  - Write integration tests for the `GET /api/sales` and `POST /api/sales` endpoints.
  - Verify that filtering and sorting work correctly.

- **Task 4.2: Frontend Unit Tests**
  - Write unit tests for the `saleService.ts` and the new React components.

- **Task 4.3: End-to-End (E2E) Testing with Cypress**
  - Create a new Cypress test file for the sales management workflow.
  - The test should log in, navigate to the sales page, add a new sale, and verify that it appears in the list.

## 4. Documentation

### Milestone 5: API Documentation

- **Task 5.1: Update API Documentation**
  - Ensure the new `/api/sales` endpoints are documented in Swagger, with clear examples.