# Epic 2: Book Management - Implementation Plan

## 1. Backend Development (.NET API)

### Milestone 1: Core API Setup

- **Task 1.1: Create Book Entity and DTOs**
  - Define the `Book` entity in `PublishingTracker.Api/Models/` with all required properties as specified in the database schema.
  - Create `BookDto`, `CreateBookDto`, and `UpdateBookDto` in `PublishingTracker.Api/Models/Dtos/` to handle data transfer between the client and server.

- **Task 1.2: Update Database Context**
  - Add a `DbSet<Book>` property to the `PublishingTrackerDbContext` in `PublishingTracker.Api/Data/`.
  - Configure the relationship between `Book` and `User` entities.

- **Task 1.3: Database Migration**
  - Generate a new EF Core migration to add the `Books` table to the database.
  - Apply the migration to update the database schema.

### Milestone 2: API Endpoint Implementation

- **Task 2.1: Map Book API Endpoints**
  - In `Program.cs` or a dedicated route extension class, map the endpoints for book management.
  - Use `MapGroup("/api/books")` to organize the book-related endpoints.
  - Implement dependency injection for `PublishingTrackerDbContext` directly in the endpoint handlers.

- **Task 2.2: Implement CRUD Endpoint Handlers**
  - **`MapGet("/", ...)`**: Handler to retrieve a list of all books for the authenticated user.
  - **`MapGet("/{id}", ...)`**: Handler to retrieve a single book by its ID.
  - **`MapPost("/", ...)`**: Handler to create a new book.
  - **`MapPut("/{id}", ...)`**: Handler to update an existing book.
  - **`MapDelete("/{id}", ...)`**: Handler to delete a book.

- **Task 2.3: Add Validation and Error Handling**
  - Implement validation for `CreateBookDto` and `UpdateBookDto` to ensure data integrity.
  - Add comprehensive error handling for all endpoints, including not found (404) and bad request (400) responses.

## 2. Frontend Development (React)

### Milestone 3: UI Component Development

- **Task 3.1: Create `bookService.ts`**
  - Create a new service in `publishing-tracker-ui/src/services/` to handle all API calls to the backend's book endpoints.

- **Task 3.2: Develop Book List Component**
  - Create a `BookListPage` component in `publishing-tracker-ui/src/pages/` to display the list of books.
  - Implement functionality to fetch and display books from the API.
  - Add search and pagination features.

- **Task 3.3: Develop Add/Edit Book Form**
  - Create a reusable `BookForm` component in `publishing-tracker-ui/src/components/` for adding and editing books.
  - Create `AddBookPage` and `EditBookPage` components that utilize the `BookForm`.
  - Implement form validation and submission logic.

## 3. Testing and Quality Assurance

### Milestone 4: Comprehensive Testing

- **Task 4.1: Backend Unit and Integration Tests**
  - Write unit tests for the business logic (if any is extracted into services).
  - Write integration tests for the Minimal API endpoints to ensure the CRUD operations work correctly from the HTTP request to the database.

- **Task 4.2: Frontend Unit Tests**
  - Write unit tests for the `bookService.ts` to mock API calls and verify data handling.
  - Write unit tests for the `BookListPage` and `BookForm` components to ensure they render and function correctly.

- **Task 4.3: End-to-End (E2E) Testing**
  - Create E2E tests that simulate the full user workflow:
    1.  Log in to the application.
    2.  Navigate to the "Books" page.
    3.  Add a new book.
    4.  Edit the details of the new book.
    5.  Delete the book.
    6.  Verify that all actions are reflected correctly in the UI.

## 4. Documentation

### Milestone 5: API Documentation

- **Task 5.1: Update API Documentation**
  - Use Swagger/OpenAPI to document the new `/api/books` endpoints.
  - Provide clear descriptions, request/response examples, and status codes for each endpoint.