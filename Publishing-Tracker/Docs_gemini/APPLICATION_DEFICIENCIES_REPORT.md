# Application Deficiencies & Adversarial Review Report

This document combines an adversarial architectural review and a strict functional audit of the Publishing Tracker application. It evaluates the application against its own specifications (`USER_STORIES.md` and `PROJECT_SUMMARY.md`) and standard software engineering principles (such as Clean Architecture).

The deficiencies are ordered by critical importance, ranging from active data loss to architectural purity.

---

## 1. Active Data Loss & Silent Failures (Critical) ✅ [RESOLVED]
**Issue:** The application silently discards user input during core operations, creating a facade of functionality.

* **Context:** Epic 2 (US-005) of `USER_STORIES.md` mandates that when adding a new book, the user must provide and save: `Title`, `Author`, `ISBN`, `Publication Date`, `Base Price`, `Genre`, and `Description`. 
* **The Failure:** While the React UI successfully renders inputs for these fields in `AddBookPage.tsx`, the C# backend completely ignores them. The `CreateBookCommandHandler` and the underlying `Book` domain entity constructor only accept `Title`, `Author`, and `ISBN`. 
* **Impact:** The remaining fields (`PublicationDate`, `BasePrice`, `Genre`, `Description`) are instantly discarded upon reaching the API. They are never saved to the database. The user experiences a "successful" save, but the data is lost forever.

## 2. Inadequate Domain Modeling & Financial Inaccuracy (Critical) ✅ [RESOLVED]
**Issue:** The system's foundational logic for financial tracking is flawed, setting the application up for inevitable mathematical inaccuracies.

* **Context:** The application is designed to track royalty revenue accurately over time across different platforms and currencies.
* **The Static Truth Fallacy:** Platform `CommissionRate` is treated as a single, static value on the `Platform` entity. If Amazon changes its commission rate from 30% to 35% tomorrow, updating the rate in the system will retroactively corrupt all historical sales calculations. The spec failed to mandate that rates must be immutable per transaction (e.g., copied to the `Sale` record at the time of purchase) or managed via an effective-dated historical table.
* **Currency Ignorance:** The spec mentions supporting multiple currencies (USD, EUR) but omits an exchange rate strategy. The dashboard currently sums revenue blindly. Summing 50 USD and 50 EUR directly into "100 Total Revenue" creates financially invalid data.

## 3. Security & Data Isolation Risks (High) ✅ [RESOLVED]
**Issue:** The application lacks necessary safeguards for multi-tenant data and user-submitted data.

* **Context:** The system is intended for multiple independent authors, requiring strict data isolation and moderation of shared resources.
* **Platform Spoofing (US-019):** The spec requests that new platform requests go into "admin review". Instead, the `POST /api/platforms` endpoint blindly creates and activates platforms instantly. A malicious user could create a fake global platform (e.g., "Amazon KDP Fake") that pollutes the database for all users.
* **Weak Multi-Tenancy:** Tenant isolation relies entirely on developers remembering to manually append `.Where(x => x.UserId == currentUser)` to queries in the Application or Infrastructure layers. A robust architecture would mandate a global query filter at the Entity Framework level to physically prevent cross-tenant data leakage.

## 4. Missing Core MVP Features (High)
**Issue:** Entire epics and features promised in the MVP are entirely absent from both the frontend and backend.

* **Context:** The `USER_STORIES.md` defines a strict set of MVP features required for launch.
* **User Profile Management (Epic 9):** `US-024` (Edit Profile Information) and `US-025` (Change Password) do not exist. There are no UI pages for account management and no backend API endpoints to support profile edits or password resets.
* **CSV Column Mapping (Epic 5, US-015):** The spec mandates a visual UI for users to map their custom CSV columns to database fields dynamically. Currently, the React app (`ImportPage.tsx`) uses a hidden, hardcoded JSON mapping. If a user uploads a differently formatted CSV, it will fail or mangle the data with no way for the user to fix the mapping.
* **Mobile Responsive Data Entry (Epic 10, US-027):** The spec calls for mobile-optimized data entry forms, but the React UI lacks meaningful responsive design effort beyond basic CSS.

## 5. Architectural Monolith & Tightly-Coupled UI (Medium)
**Issue:** The recent "Clean Architecture" rewrite is structurally unenforced on the backend and completely absent on the frontend.

* **Context:** The project aims to showcase an enterprise-grade Clean Architecture (Domain, Application, Infrastructure, Presentation).
* **The Backend "Honor System" Monolith:** We organized the C# code into logical folders (`Domain`, `Application`, `Infrastructure`), but kept them inside a single monolithic project (`PublishingTracker.Api.csproj`). Because physical project boundaries weren't drawn, the compiler cannot stop a developer from violating dependency rules (e.g., accidentally importing `Microsoft.EntityFrameworkCore` directly into a Domain entity).
* **The Tightly-Coupled React Frontend:** The React frontend was left completely out of the architecture rewrite. It remains a standard 3-tier app where UI components (e.g., `BookListPage.tsx`) are tightly coupled directly to Axios services (`bookService.ts`). It completely lacks "Ports and Adapters" (Hexagonal UI). If the backend API changes from REST to GraphQL, or if data needed to be cached in LocalStorage, the React components themselves would have to be rewritten.

## 6. Missing Infrastructure Components (Low)
**Issue:** The application lacks the backend infrastructure to support its advanced features.

* **Context:** The spec references cloud-native background processing and user notifications.
* **No Email System:** Features like "email notification on import completion" (Epic 8, US-022) and "email confirmation of password change" (Epic 9, US-025) cannot function because there is zero email infrastructure (e.g., SendGrid, MailKit) or notification logic in the codebase.
* **Background Processing Compromise:** The original spec explicitly mentioned "Azure Functions (Background Processing)". We implemented the CSV import using an in-memory `BackgroundService` channel within the main API project. While this works for a portfolio, it falls short of the cloud-native, serverless architecture promised. If the API server scales horizontally to multiple instances, multiple competing background workers reading from in-memory queues will lead to lost jobs or race conditions.
