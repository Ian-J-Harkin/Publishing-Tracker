# Implementation Plan: Phase 1.5 - Backend Refactoring & Infrastructure

While the core Clean Architecture monolith split (Phase 1) is complete, there is outstanding backend technical debt and missing infrastructure that should be addressed before we completely shift focus to the React frontend in Phase 2.

This document serves as the master plan for the remaining backend work.

## Open Questions
- **Email Provider:** For the email infrastructure (Item 6), do you want to implement a real provider like SendGrid/Mailgun, or a mock `ILogger` based `IEmailService` for the MVP?
- **Background Jobs:** Do you want to keep the in-memory `Channel<T>` background service for CSV imports, or migrate to a more robust job queue (e.g., Hangfire or Azure Functions) as mentioned in the original spec?

## Proposed Changes

### 1. Extract Dashboard Endpoints to MediatR (Technical Debt)
Currently, `DashboardEndpoints.cs` contains complex, inline Entity Framework queries and data aggregation logic. This violates the Clean Architecture pattern established in Phase 1.

#### [MODIFY] `PublishingTracker.Api/Endpoints/DashboardEndpoints.cs`
- Strip out all inline `DbContext` logic.
- Route `/all` and `/summary` endpoints to MediatR `Send()`.

#### [NEW] `PublishingTracker.Application/Dashboard/Queries/`
- Create `GetDashboardDataQuery` and `GetDashboardDataQueryHandler`.
- Create `GetDashboardSummaryQuery` and `GetDashboardSummaryQueryHandler`.
- Move the currency grouping, Year-Over-Year, and seasonal aggregation logic into these handlers.

### 2. Complete Application Deficiency Item 6 (Missing Infrastructure)
As identified in `Docs_gemini/APPLICATION_DEFICIENCIES_REPORT.md`, the backend lacks the infrastructure promised in the original MVP specifications.

#### [NEW] `PublishingTracker.Application/Common/Interfaces/IEmailService.cs`
- Define an interface for sending emails (e.g., password change confirmations, import completion notifications).

#### [NEW] `PublishingTracker.Infrastructure/Services/EmailService.cs`
- Implement the `IEmailService`.

#### [MODIFY] `PublishingTracker.Application/Users/Commands/ChangePasswordCommandHandler.cs`
- Inject `IEmailService` and trigger an email notification upon a successful password change (US-025).

#### [MODIFY] `PublishingTracker.Infrastructure/Services/ImportBackgroundService.cs`
- Inject `IEmailService` and trigger an email notification when a background CSV import job completes or fails (US-022).

## Verification Plan

### Automated Tests
- Run `dotnet test` to ensure the Dashboard endpoints still return the exact same JSON schema after being extracted to MediatR.
- Add unit tests for the newly created `GetDashboardDataQueryHandler`.
- Verify the `IEmailService` is correctly registered in the DI container and successfully mock-fires during the Integration tests.
