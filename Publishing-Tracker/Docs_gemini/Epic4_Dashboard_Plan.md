# Epic 4: Dashboard & Analytics (MVP) - Implementation Plan

## 1. Backend Development (.NET API)

### Milestone 1: Dashboard API Endpoints

- **Task 1.1: Create Dashboard DTOs**
  - Define `DashboardSummaryDto`, `RevenueChartDataDto`, and `PlatformPerformanceDto` in `PublishingTracker.Api/Models/Dtos/` to structure the data for the dashboard.

- **Task 1.2: Map Dashboard API Endpoints**
  - In `Program.cs`, create a new route group for `/api/dashboard`.
  - Implement the following endpoints:
    - **`GET /summary`**: Returns the `DashboardSummaryDto` with key performance indicators.
    - **`GET /revenue-chart`**: Returns the `RevenueChartDataDto` for the revenue chart, with a parameter for the time period.
    - **`GET /platform-performance`**: Returns the `PlatformPerformanceDto` with a breakdown of revenue by platform.

- **Task 1.3: Implement Data Aggregation Logic**
  - In the endpoint handlers, write LINQ queries to efficiently aggregate sales data from the database.
  - Ensure the queries are optimized for performance.

## 2. Frontend Development (React)

### Milestone 2: UI Component Development

- **Task 2.1: Create `dashboardService.ts`**
  - Create a new service in `publishing-tracker-ui/src/services/` to handle API calls to the dashboard endpoints.

- **Task 2.2: Develop `DashboardPage` Component**
  - Enhance the existing `DashboardPage` component in `publishing-tracker-ui/src/pages/` to fetch and display the dashboard data.
  - Create reusable components for displaying KPIs, the revenue chart, and the platform performance summary.

- **Task 2.3: Integrate Charting Library**
  - Integrate a charting library (e.g., Recharts or Chart.js) to visualize the revenue and platform performance data.
  - Ensure the charts are responsive and interactive.

## 3. Testing and Quality Assurance

### Milestone 3: Comprehensive Testing

- **Task 3.1: Backend Integration Tests**
  - Write integration tests for the new dashboard API endpoints.
  - Verify the accuracy of the aggregated data returned by the endpoints.

- **Task 3.2: Frontend Unit Tests**
  - Write unit tests for the `dashboardService.ts` and the new dashboard components.

- **Task 3.3: End-to-End (E2E) Testing with Cypress**
  - Create a new Cypress test file for the dashboard.
  - The test should log in, navigate to the dashboard, and verify that the key metrics and charts are displayed correctly.

## 4. Documentation

### Milestone 4: API Documentation

- **Task 4.1: Update API Documentation**
  - Ensure the new `/api/dashboard` endpoints are documented in Swagger, with clear examples of the expected request and response formats.