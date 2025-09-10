# Epic 5: CSV Import (Basic) - Implementation Plan

## 1. Backend Development (.NET API)

### Milestone 1: Core API Setup

- **Task 1.1: Create ImportJob Entity and DTOs**
  - Define the `ImportJob` entity in `PublishingTracker.Api/Models/` to track the status of CSV imports.
  - Create `ImportJobDto`, `FileUploadDto`, and `ColumnMappingDto` in `PublishingTracker.Api/Models/Dtos/`.

- **Task 1.2: Update Database Context**
  - Add a `DbSet<ImportJob>` property to the `PublishingTrackerDbContext`.

- **Task 1.3: Database Migration**
  - Generate and apply a new EF Core migration to add the `ImportJobs` table.

### Milestone 2: API Endpoint Implementation

- **Task 2.1: Map Import API Endpoints**
  - In `Program.cs`, create a new route group for `/api/import`.
  - Implement the following endpoints:
    - **`POST /upload`**: Handles the initial CSV file upload.
    - **`POST /process`**: Processes the uploaded file with the user-defined column mappings.
    - **`GET /history`**: Retrieves a list of past import jobs.

- **Task 2.2: Implement CSV Parsing and Processing Logic**
  - Create a new `CsvImportService` to handle the business logic for parsing CSV files, mapping columns, and creating `Sale` and `Book` records.
  - Use a lightweight CSV parsing library (e.g., CsvHelper) to handle file reading.
  - Implement logic to handle potential errors during processing and log them to the `ImportJob` record.

## 2. Frontend Development (React)

### Milestone 3: UI Component Development

- **Task 3.1: Create `importService.ts`**
  - Create a new service in `publishing-tracker-ui/src/services/` for the import-related API calls.

- **Task 3.2: Develop Import Page Components**
  - Create an `ImportPage` component that guides the user through the three main steps:
    1.  **File Upload:** A component to select and upload the CSV file.
    2.  **Column Mapping:** A component to map CSV columns to the required fields.
    3.  **Import Summary:** A component to display the results of the import.
  - Create an `ImportHistoryPage` to display the list of past import jobs.

## 3. Testing and Quality Assurance

### Milestone 4: Comprehensive Testing

- **Task 4.1: Backend Unit and Integration Tests**
  - Write unit tests for the `CsvImportService`.
  - Write integration tests for the `/api/import` endpoints, using sample CSV files.

- **Task 4.2: Frontend Unit Tests**
  - Write unit tests for the new import-related components and services.

- **Task 4.3: End-to-End (E2E) Testing with Cypress**
  - Create a new Cypress test file for the CSV import workflow.
  - The test should log in, upload a sample CSV, map the columns, process the import, and verify that the new sales data appears on the sales page.

## 4. Documentation

### Milestone 5: API Documentation

- **Task 5.1: Update API Documentation**
  - Ensure the new `/api/import` endpoints are documented in Swagger, with clear examples.