# Current Test Guide: Clean Architecture & CQRS

This guide matches the current backend test suite for PublishingTracker.

## What the suite covers

- Auth handler unit tests
- API integration tests for auth, books, platforms, sales, profile, users, and import
- CSV import flow with background processing
- Role-based authorization checks
- Currency conversion and sale history behavior

## Test stack

- Target framework: `net9.0`
- xUnit for tests
- `Microsoft.AspNetCore.Mvc.Testing` for API integration tests
- `Microsoft.EntityFrameworkCore.InMemory` for isolated handler tests
- LocalDB SQL Server for API integration tests

## How the integration tests are wired

`TestWebAppFactory` replaces the app DbContext with:

`Server=(localdb)\mssqllocaldb;Database=PublishingTracker_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true`

The factory also:

- runs the app in the `Testing` environment
- resets the test database once per run
- seeds an admin user: `test@test.com` / `password123`
- generates bearer tokens and antiforgery tokens for authenticated requests

## Current integration coverage

### Auth

- register with a new email returns success
- duplicate registration returns bad request
- login with valid credentials returns success
- login with invalid credentials returns unauthorized

### Books

- authenticated `GET /api/books` returns success
- authenticated `POST /api/books` returns created
- update flow preserves optional fields

### Platforms

- authenticated `GET /api/platforms` returns success
- platform request returns created
- standard users are forbidden from admin-only platform approval routes

### Sales

- sale creation captures historical commission rate
- foreign-currency sales are normalized to base currency
- commission changes do not corrupt historical sales

### Profile / Users

- profile endpoints return the current user
- admin-only user listing is blocked for standard users

### Import

- upload returns preview data
- process returns `202 Accepted`
- status polling reaches `Completed`
- history includes the queued job
- imported books, platforms, and sales are written to the database

## Running the tests

From `apps/api`:

```powershell
dotnet test .\PublishingTracker.Api\PublishingTracker.Api.sln
```

## Current status

As of `2026-08-22`, the backend test suite passes (`38/38`).
