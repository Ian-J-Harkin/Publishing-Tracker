# Application Deficiencies: Work Done and TO DOs

This document serves as a waypoint summarizing the progress made against the Application Deficiencies Report and outlining the outstanding work.

## ✅ Work Completed (Part 1 - Bug Fixes)

We successfully addressed **Item 1 (Inadequate Domain Modeling)** and **Item 2 (Financial Inaccuracy)** from the deficiency report:

### Domain Layer
- Expanded the `Sale` entity to include `PlatformCommissionRate`, `ExchangeRate`, `BaseCurrencyRevenue`, and `BaseCurrencyRoyalty`.
- Updated the `Sale.Create` factory method to ensure these historical financial snapshots are accurately captured at the moment of sale creation.

### Application Layer
- Created `ICurrencyConverterService` to handle multi-currency logic.
- Updated `CreateSaleCommandHandler` to utilize the converter service, calculating and persisting the normalized base currency values automatically.
- Fixed `GetSalesSummaryQueryHandler` and other aggregation queries to sum against `BaseCurrencyRevenue` instead of the raw `Revenue`, preventing inaccurate blind sums of mixed currencies (e.g., mixing GBP, EUR, and USD).
- Updated the `ImportCsvCommand` to cleanly inject platform rates and convert currencies during batch uploads.

### Infrastructure & Persistence
- Implemented `StaticCurrencyConverterService` with standard conversion rates.
- Updated EF Core configuration in `PublishingTrackerDbContext` with correct precision for the new decimal columns.
- Successfully generated and applied the `FixDomainModelingAndCurrency` EF Core Migration.
- Fixed duplicated and mangled files (`GetSalesSummaryQuery.cs`, `ImportCsvCommand.cs`, `BookRepository.cs`) back to a pristine compiling state.

### Verification (Integration Tests)
- Appended robust integration tests to `IntegrationTests.cs`:
  1. `CreateSale_WithForeignCurrency_ConvertsToBaseCurrency`: Validates that foreign currencies (e.g., EUR) correctly calculate and store the normalized USD value.
  2. `CreateSale_CommissionRateChange_DoesNotCorruptHistory`: Proves that a change to a Platform's commission rate does not retroactively corrupt historical sales records.
- **Status:** All 17 integration tests are compiling and passing cleanly (`dotnet test`).

---

## ⏳ TO DO (Part 2 and Beyond)

The following items are outstanding and ready to be tackled next:

### 1. Integration Test Infrastructure ✅ [RESOLVED]
- **Replaced `InMemoryDatabase`:** We successfully removed the `InMemoryDatabase` provider.
- **Action Taken:** The integration tests now point to the LocalDB SQL Server instance (`PublishingTracker_IntegrationTests`), ensuring tests execute against a real relational database. Database state is wiped and isolated between each test run.

### 2. Remaining Deficiencies (Items 3 - 6)
- The initial report contained 6 items ordered by importance. Now that items 1 and 2 are fully implemented, we need to sequentially address the remaining 4 items from the original `APPLICATION_DEFICIENCIES_REPORT.md`.
- *Note: Before starting on these, we will review the exact details of items 3-6 to formulate a focused implementation plan.*

### 3. Application Hardening
- Ensure that the new API endpoints (`BookEndpoints`, `PlatformEndpoints`) properly handle edge cases related to the new fields (e.g., missing currency codes fallback to USD).
