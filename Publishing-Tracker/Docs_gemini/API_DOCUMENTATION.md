# API Documentation

This document provides a comprehensive overview of the Publishing Tracker API endpoints.

## 1. Authentication

### POST /api/auth/register

Registers a new user.

-   **Request Body:** `RegisterRequest`
    -   `email` (string, required)
    -   `password` (string, required)
    -   `firstName` (string, required)
    -   `lastName` (string, required)
-   **Responses:**
    -   `200 OK`: Returns an `AuthResponse` with a JWT token.
    -   `400 Bad Request`: If the email already exists.

### POST /api/auth/login

Logs in an existing user.

-   **Request Body:** `LoginRequest`
    -   `email` (string, required)
    -   `password` (string, required)
-   **Responses:**
    -   `200 OK`: Returns an `AuthResponse` with a JWT token.
    -   `401 Unauthorized`: If the credentials are invalid.
## 2. Book Management

All endpoints in this section require authentication.

### GET /api/books

Retrieves a list of all books for the authenticated user.

-   **Responses:**
    -   `200 OK`: Returns an array of `Book` objects.

### GET /api/books/{id}

Retrieves a specific book by its ID.

-   **Parameters:**
    -   `id` (integer, required): The ID of the book.
-   **Responses:**
    -   `200 OK`: Returns the `Book` object.
    -   `404 Not Found`: If the book is not found.

### POST /api/books

Creates a new book.

-   **Request Body:** `CreateBookDto`
    -   `title` (string, required)
    -   `author` (string, required)
    -   `isbn` (string)
    -   `publicationDate` (date)
    -   `basePrice` (decimal)
    -   `genre` (string)
    -   `description` (string)
-   **Responses:**
    -   `201 Created`: Returns the newly created `Book` object.

### PUT /api/books/{id}

Updates an existing book.

-   **Parameters:**
    -   `id` (integer, required): The ID of the book.
-   **Request Body:** `UpdateBookDto` (same as `CreateBookDto`, but all fields are optional)
-   **Responses:**
    -   `200 OK`: Returns the updated `Book` object.
    -   `404 Not Found`: If the book is not found.

### DELETE /api/books/{id}

Deletes a book.

-   **Parameters:**
    -   `id` (integer, required): The ID of the book.
-   **Responses:**
    -   `204 No Content`: If the book was successfully deleted.
    -   `404 Not Found`: If the book is not found.
## 3. Sales Management

All endpoints in this section require authentication.

### GET /api/sales

Retrieves a list of all sales for the authenticated user.

-   **Responses:**
    -   `200 OK`: Returns an array of `SaleDto` objects.

### POST /api/sales

Creates a new sale.

-   **Request Body:** `CreateSaleDto`
    -   `bookId` (integer, required)
    -   `platformId` (integer, required)
    -   `saleDate` (date, required)
    -   `quantity` (integer, required)
    -   `unitPrice` (decimal, required)
    -   `royalty` (decimal, required)
-   **Responses:**
    -   `201 Created`: Returns the newly created `Sale` object.
    -   `404 Not Found`: If the book is not found or does not belong to the user.
## 4. Dashboard

All endpoints in this section require authentication.

### GET /api/dashboard/summary

Retrieves a summary of key metrics for the authenticated user.

-   **Responses:**
    -   `200 OK`: Returns a `DashboardSummaryDto` object.
## 5. CSV Import

All endpoints in this section require authentication.

### POST /api/import/upload

Uploads a CSV file for processing.

-   **Request Body:** `IFormFile`
-   **Responses:**
    -   `200 OK`: Returns an object with the file name.

### POST /api/import/process

Processes a previously uploaded CSV file.

-   **Request Body:** `ColumnMappingDto`
-   **Responses:**
    -   `200 OK`: Returns a message indicating that the import process has started.

### GET /api/import/history

Retrieves the import history for the authenticated user.

-   **Responses:**
    -   `200 OK`: Returns an array of `ImportJob` objects.
## 6. Platform Management

All endpoints in this section require authentication.

### GET /api/platforms

Retrieves a list of all platforms.

-   **Responses:**
    -   `200 OK`: Returns an array of `PlatformDto` objects.

### POST /api/platforms/requests

Submits a request for a new platform.

-   **Request Body:** `PlatformRequestDto`
    -   `name` (string, required)
    -   `baseUrl` (string)
    -   `commissionRate` (decimal, required)
-   **Responses:**
    -   `201 Created`: Returns the newly created `PlatformRequest` object.