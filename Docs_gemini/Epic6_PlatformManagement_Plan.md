# Epic 6: Platform Management - Detailed Implementation Plan

## 1. Backend Development (.NET API)

### Milestone 1: API Endpoint Implementation

- **Task 1.1: Create Platform DTOs**
  - In `PublishingTracker.Api/Models/Dtos/`, create a new file `PlatformDto.cs`.
  - Define `PlatformDto` to represent the data sent to the client (Id, Name, BaseUrl, CommissionRate).
  - Define `PlatformRequestDto` for the new platform request form (Name, BaseUrl, CommissionRate).

- **Task 1.2: Create PlatformRequest Entity**
  - In `PublishingTracker.Api/Models/`, create a new file `PlatformRequest.cs`.
  - Define the `PlatformRequest` entity with properties for Id, Name, BaseUrl, CommissionRate, and UserId.

- **Task 1.3: Update Database Context**
  - In `PublishingTrackerDbContext.cs`, add a `DbSet<PlatformRequest>` property.

- **Task 1.4: Database Migration**
  - Generate a new EF Core migration to add the `PlatformRequests` table.
  - Apply the migration to the database.

- **Task 1.5: Implement API Endpoints**
  - In `Program.cs`, create a new route group for `/api/platforms`.
  - The `GET /` endpoint should be enhanced to return a list of `PlatformDto`.
  - Create a new `POST /requests` endpoint that accepts a `PlatformRequestDto` and saves a new `PlatformRequest` to the database.

## 2. Frontend Development (React)

### Milestone 2: UI Component Development

- **Task 2.1: Create Platform Types**
  - In `publishing-tracker-ui/src/types/`, create a new file `platform.ts`.
  - Define the `Platform` and `PlatformRequest` interfaces.

- **Task 2.2: Create `platformService.ts`**
  - In `publishing-tracker-ui/src/services/`, create a new file `platformService.ts`.
  - Implement functions to `getPlatforms` and `requestPlatform`.

- **Task 2.3: Develop `PlatformsPage.tsx`**
  - In `publishing-tracker-ui/src/pages/`, create a new file `PlatformsPage.tsx`.
  - This component will fetch and display the list of platforms.
  - Include a link to the "Request New Platform" page.

- **Task 2.4: Develop `RequestPlatformPage.tsx`**
  - In `publishing-tracker-ui/src/pages/`, create a new file `RequestPlatformPage.tsx`.
  - This component will contain the form for submitting new platform requests.

- **Task 2.5: Update App Routing**
  - In `App.tsx`, add new routes for `/platforms` and `/platforms/request`.

## 3. Testing and Quality Assurance

### Milestone 3: Comprehensive Testing

- **Task 3.1: Backend Integration Tests**
  - Create `PlatformEndpointsTests.cs` in the test project.
  - Write tests for the `GET /api/platforms` and `POST /api/platforms/requests` endpoints.

- **Task 3.2: Frontend Unit Tests**
  - Write unit tests for the new platform-related services and components.

- **Task 3.3: End-to-End (E2E) Testing**
  - Create a new Cypress test file, `platform_management.cy.ts`.
  - The test will log in, navigate to the platforms page, and submit a new platform request.

## 4. Documentation

### Milestone 4: API Documentation

- **Task 4.1: Update API Documentation**
  - Ensure the new `/api/platforms/requests` endpoint is documented in Swagger.