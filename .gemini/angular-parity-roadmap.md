# Angular ↔ React Parity Roadmap

**Created:** 2026-02-10
**Goal:** Bring the Angular frontend to full feature and UI parity with the React version.

---

## Overview

The work is organised into **3 phases**, ordered by dependency and complexity:

| Phase | Focus | Tasks | Est. Effort |
|-------|-------|-------|-------------|
| **Phase 1** | Infrastructure & Models | 6 tasks | Low |
| **Phase 2** | Update Existing Components | 5 tasks | Medium |
| **Phase 3** | Create Missing Features | 8 tasks | High |

**Total: 19 tasks**

---

## Phase 1 — Infrastructure & Data Layer

These tasks create the Angular services, models, and routing scaffolding that Phase 2 and Phase 3 depend on. They have no UI and can be done quickly.

### Task 1.1 — Create `sale.ts` model
- **Location:** `src/app/core/models/sale.ts`
- **Port from:** `ui-react/src/types/sale.ts`
- **Interfaces to create:**
  - `Sale` (id, bookId, bookTitle, platformId, platformName, saleDate, quantity, unitPrice, royalty, revenue)
  - `CreateSale` (bookId, platformId, saleDate, quantity, unitPrice, royalty, currency)
  - `SalesSummary` (totalRevenue, totalUnitsSold, averageRoyalty, salesCount)
  - `SalesFilter` (bookId?, platformId?, startDate?, endDate?)
- **Tests:** None (pure type definitions)
- **Blocked by:** Nothing

### Task 1.2 — Create `import.ts` model
- **Location:** `src/app/core/models/import.ts`
- **Port from:** `ui-react/src/types/import.ts`
- **Interfaces to create:**
  - `ImportJob` (id, fileName, status, startedAt, completedAt?, recordsProcessed, recordsSuccessful, recordsFailed, errorLog?)
  - `ColumnMapping` (bookTitle, platform, saleDate, quantity, unitPrice, royalty, revenue, currency, orderId, defaultCurrency?)
  - `PreviewData` (fileName, headers, previewRows)
- **Tests:** None (pure type definitions)
- **Blocked by:** Nothing

### Task 1.3 — Create `sale.service.ts`
- **Location:** `src/app/core/services/sale.service.ts`
- **Port from:** `ui-react/src/services/saleService.ts`
- **API endpoints to call:**
  - `GET /api/sales` (with query params from `SalesFilter`)
  - `GET /api/sales/summary` (with date range params)
  - `POST /api/sales` (body: `CreateSale`)
- **Methods:** `getSales(filters)`, `getSummary(filters)`, `createSale(sale)`
- **Tests:** Create `sale.service.spec.ts` with HttpClientTestingModule mocks
- **Blocked by:** Task 1.1

### Task 1.4 — Create `import.service.ts`
- **Location:** `src/app/core/services/import.service.ts`
- **Port from:** `ui-react/src/services/importService.ts`
- **API endpoints to call:**
  - `POST /api/import/upload` (multipart/form-data with file)
  - `POST /api/import/process` (body: `{ fileName, mapping }`)
  - `GET /api/import/history`
- **Methods:** `uploadFile(file)`, `processFile(fileName, mapping)`, `getHistory()`
- **Tests:** Create `import.service.spec.ts` with HttpClientTestingModule mocks
- **Blocked by:** Task 1.2

### Task 1.5 — Register routes for Sales & Import
- **Location:** `src/app/app.routes.ts`
- **Changes:**
  - Add route `sales` → `SalesListComponent`
  - Add route `sales/add` → `AddSaleComponent`
  - Add route `import` → `ImportComponent`
  - Add route `import/history` → `ImportHistoryComponent`
  - All routes inside the `MainLayoutComponent` children (guarded by `AuthGuard`)
- **Note:** The `main-layout.component.html` already has `Sales` and `Import` nav links pointing to `/sales` and `/import`, so navigation will "just work" once routes are registered.
- **Blocked by:** Phase 3 components (can be added incrementally as each component is built)

### Task 1.6 — Add `Import History` nav link
- **Location:** `src/app/shared/components/main-layout/main-layout.component.html`
- **Changes:**
  - The current nav has `Import` linking to `/import`. Consider adding a sub-link or secondary route for `/import/history`. React handles this via a separate route.
  - Alternatively, the Import page itself can link to History (matching the React pattern).
- **Blocked by:** Nothing

---

## Phase 2 — Update Existing Components

These tasks bring already-ported Angular components up to feature and UI parity with their React counterparts.

### Task 2.1 — Enhance `BookListComponent`
- **Location:** `src/app/features/book/book-list/`
- **Reference:** `ui-react/src/pages/BookListPage.tsx` (233 lines)
- **Current state (Angular):** Basic `<ul>` list with title, author, delete button. 21 lines of HTML.
- **Changes required:**
  - [ ] Add **search bar** with debounced input (use `Subject` + `debounceTime(500)` from RxJS)
  - [ ] Replace `<ul>/<li>` with a **flex-based table layout** (header row + data rows)
  - [ ] Add **"Genre"** column with badge styling
  - [ ] Add **"Publication Date"** column with formatted date display
  - [ ] Add **"Performance"** link (📊 icon) per row, routing to `/books/:id/performance`
  - [ ] Add **empty state** ("Portfolio is Empty" message with icon)
  - [ ] Add **loading skeleton/shimmer** state
  - [ ] Add **hover effects** on rows (CSS: `box-shadow`, `inset border`)
  - [ ] Update `book-list.component.css` with all new styles
- **Update spec:** Update `book-list.component.spec.ts` to verify search, empty state, genre display
- **Blocked by:** Nothing

### Task 2.2 — Enhance `PlatformsComponent`
- **Location:** `src/app/features/platforms/`
- **Reference:** `ui-react/src/pages/PlatformsPage.tsx` (203 lines)
- **Current state (Angular):** Basic `<ul>` list with name, baseUrl, commissionRate. 20 lines of HTML.
- **Changes required:**
  - [ ] Add **search bar** with debounced input
  - [ ] Replace `<ul>` with a **CSS Grid** of platform cards
  - [ ] Each card should display:
    - **Platform icon** (first letter of name, gradient background)
    - **Platform name** (styled heading)
    - **Merchant Fee** (percentage badge in green)
    - **Network URL** (truncated link)
  - [ ] Add **empty state** with "Request Support" link
  - [ ] Add **loading skeleton/shimmer**
  - [ ] Add **card hover effects** (translateY, border glow, shadow)
  - [ ] Modify `platform.service.ts` to accept optional `search` parameter (if not already)
  - [ ] Update `platforms.component.css` with card grid and animation styles
- **Update spec:** Update `platforms.component.spec.ts` to verify search, card rendering
- **Blocked by:** Nothing

### Task 2.3 — Add CSV Export to `BookPerformanceComponent`
- **Location:** `src/app/features/book/book-performance/`
- **Reference:** `ui-react/src/pages/BookPerformancePage.tsx` (55 lines)
- **Current state (Angular):** Renders performance list but has no export functionality.
- **Changes required:**
  - [ ] Add **"Export to CSV"** button/link
  - [ ] Implement CSV generation logic (no external library needed — use `Blob` + `URL.createObjectURL` + `<a download>` pattern)
  - [ ] CSV columns: `Platform Name`, `Currency`, `Total Revenue`
  - [ ] Style the export button to match app theme
- **Update spec:** Add test verifying the export button renders
- **Blocked by:** Nothing

### Task 2.4 — Verify `AddBookComponent` parity
- **Location:** `src/app/features/book/add-book/`
- **Reference:** `ui-react/src/pages/AddBookPage.tsx` (159 lines)
- **Audit checklist:**
  - [ ] Verify form fields match React version (title, author, publicationDate, genre, isbn)
  - [ ] Verify validation messages match
  - [ ] Verify success/error feedback
  - [ ] Verify navigation on success (`/books`)
- **Blocked by:** Nothing

### Task 2.5 — Verify `EditBookComponent` parity
- **Location:** `src/app/features/book/edit-book/`
- **Reference:** `ui-react/src/pages/EditBookPage.tsx` (190 lines)
- **Audit checklist:**
  - [ ] Verify pre-population of form fields from API
  - [ ] Verify update submission
  - [ ] Verify error handling
  - [ ] Verify navigation on success
- **Blocked by:** Nothing

---

## Phase 3 — Create Missing Features

These are entirely new feature modules that must be built from scratch in Angular, porting the logic and UI from the corresponding React pages.

### Task 3.1 — Create `SalesListComponent`
- **Location:** `src/app/features/sales/sales-list/`
- **Port from:** `ui-react/src/pages/SalesPage.tsx` (257 lines)
- **Files to create:**
  - `sales-list.component.ts`
  - `sales-list.component.html`
  - `sales-list.component.css`
  - `sales-list.component.spec.ts`
- **Features to implement:**
  - [ ] **Summary ribbon** — 3 metric cards: Total Revenue, Global Unit Sales, Avg Royalty/Unit
  - [ ] **Filter bar** — dropdowns for Book (populated from `BookService`), Platform (from `PlatformService`), date range inputs
  - [ ] **Data table** with flex-based header and scrollable body
  - [ ] Table columns: Date, Title, Channel (badge), Qty, Royalty, Revenue
  - [ ] **Empty state** ("No matching sales found")
  - [ ] **Loading shimmer**
  - [ ] **Row hover effects**
  - [ ] **"Add Manual Entry"** button linking to `/sales/add`
  - [ ] Filters should reactively re-fetch data (use `BehaviorSubject` or reactive forms)
- **Blocked by:** Task 1.1, Task 1.3

### Task 3.2 — Create `AddSaleComponent`
- **Location:** `src/app/features/sales/add-sale/`
- **Port from:** `ui-react/src/pages/AddSalePage.tsx` (250 lines)
- **Files to create:**
  - `add-sale.component.ts`
  - `add-sale.component.html`
  - `add-sale.component.css`
  - `add-sale.component.spec.ts`
- **Features to implement:**
  - [ ] **Reactive form** (FormGroup) with fields:
    - Book (dropdown from `BookService`)
    - Platform (dropdown from `PlatformService`)
    - Transaction Date (date picker)
    - Volume/Quantity (number)
    - Reporting Currency (dropdown: USD, GBP, EUR, CAD, AUD)
    - Market Price (number, step 0.01)
    - Accrued Royalty (number, step 0.01)
  - [ ] **Dynamic "Net Projected Yield"** display (Quantity × Royalty), updating on input change
  - [ ] **Validation** — require book and platform selection
  - [ ] **Submit** → calls `SaleService.createSale()` → navigates to `/sales`
  - [ ] **"Discard Entry"** button → navigates to `/sales`
  - [ ] **Error banner** on failure
- **Blocked by:** Task 1.1, Task 1.3

### Task 3.3 — Create `ImportComponent` (Multi-step Wizard)
- **Location:** `src/app/features/import/import/`
- **Port from:** `ui-react/src/pages/ImportPage.tsx` (348 lines)
- **Files to create:**
  - `import.component.ts`
  - `import.component.html`
  - `import.component.css`
  - `import.component.spec.ts`
- **Features to implement (3-step wizard):**
  - [ ] **Step 1 — Upload:** File input (drag-and-drop zone or browse), file type validation (.csv), "Next: Map Columns" button
  - [ ] **Step 2 — Map Columns:** Display detected CSV headers. For each required field (Book Title, Platform, Sale Date, Quantity, Unit Price), show a dropdown to select the matching CSV header. Optional fields: Royalty, Revenue, Currency, Order ID. Default currency selector. "Process File" button.
  - [ ] **Step 3 — Summary:** Display import results:
    - Records Processed / Successful / Failed counts
    - Success Rate percentage
    - Diagnostic Audit Trail (error log in `<pre>` block)
    - "Import Another File" and "View History" buttons
  - [ ] **Step indicator** (visual breadcrumbs: Upload → Map → Summary)
  - [ ] **Progress/loading** state during file upload and processing
  - [ ] **Error handling** for upload and process failures
- **Blocked by:** Task 1.2, Task 1.4

### Task 3.4 — Create `ImportHistoryComponent`
- **Location:** `src/app/features/import/import-history/`
- **Port from:** `ui-react/src/pages/ImportHistoryPage.tsx` (194 lines)
- **Files to create:**
  - `import-history.component.ts`
  - `import-history.component.html`
  - `import-history.component.css`
  - `import-history.component.spec.ts`
- **Features to implement:**
  - [ ] **Table view** with columns: Import Details (filename + date), Status (badge), Performance (progress bar), Actions
  - [ ] **Status badges** — colour-coded by status (Completed=green, Failed=red, Processing=yellow)
  - [ ] **Progress bar** — visual success rate (green/yellow/red based on percentage)
  - [ ] **Expandable error logs** — "View Logs" / "Hide Logs" toggle per row, log displayed in dark `<pre>` block
  - [ ] **Empty state** ("No synchronization history found")
  - [ ] **Loading state**
- **Blocked by:** Task 1.2, Task 1.4

### Task 3.5 — Write unit tests for `SalesListComponent`
- **Location:** `src/app/features/sales/sales-list/sales-list.component.spec.ts`
- **Coverage:**
  - [ ] Renders summary cards with mocked data
  - [ ] Filter dropdowns populated from services
  - [ ] Sales rows render correctly
  - [ ] Empty state displays when no sales
- **Blocked by:** Task 3.1

### Task 3.6 — Write unit tests for `AddSaleComponent`
- **Location:** `src/app/features/sales/add-sale/add-sale.component.spec.ts`
- **Coverage:**
  - [ ] Form renders with all fields
  - [ ] Validation errors display (missing book/platform)
  - [ ] Projected yield updates dynamically
  - [ ] Successful submission calls service and navigates
- **Blocked by:** Task 3.2

### Task 3.7 — Write unit tests for `ImportComponent`
- **Location:** `src/app/features/import/import/import.component.spec.ts`
- **Coverage:**
  - [ ] Step 1 renders upload UI
  - [ ] File selection enables "Next" button
  - [ ] Step 2 renders column mapping dropdowns
  - [ ] Step 3 renders summary with correct numbers
- **Blocked by:** Task 3.3

### Task 3.8 — Write unit tests for `ImportHistoryComponent`
- **Location:** `src/app/features/import/import-history/import-history.component.spec.ts`
- **Coverage:**
  - [ ] Table renders with mocked history data
  - [ ] Status badges have correct styles
  - [ ] Error log toggle works
  - [ ] Empty state renders
- **Blocked by:** Task 3.4

---

## Execution Order (Recommended)

```
Phase 1 (Infrastructure)
├── Task 1.1  Create sale.ts model
├── Task 1.2  Create import.ts model
├── Task 1.3  Create sale.service.ts         (depends on 1.1)
├── Task 1.4  Create import.service.ts       (depends on 1.2)
├── Task 1.5  Register routes                (incremental, as components are built)
└── Task 1.6  Update nav links               (trivial)

Phase 2 (Update Existing) — can run in parallel with Phase 1
├── Task 2.1  Enhance BookListComponent
├── Task 2.2  Enhance PlatformsComponent
├── Task 2.3  Add CSV Export to BookPerformance
├── Task 2.4  Verify AddBookComponent
└── Task 2.5  Verify EditBookComponent

Phase 3 (New Features) — depends on Phase 1
├── Task 3.1  Create SalesListComponent      (depends on 1.1, 1.3)
├── Task 3.2  Create AddSaleComponent        (depends on 1.1, 1.3)
├── Task 3.3  Create ImportComponent         (depends on 1.2, 1.4)
├── Task 3.4  Create ImportHistoryComponent  (depends on 1.2, 1.4)
├── Task 3.5  Tests for SalesList            (depends on 3.1)
├── Task 3.6  Tests for AddSale              (depends on 3.2)
├── Task 3.7  Tests for Import               (depends on 3.3)
└── Task 3.8  Tests for ImportHistory        (depends on 3.4)
```

---

## Key Technical Notes

1. **No Virtual Scrolling needed initially.** React uses `@tanstack/react-virtual` for large lists. Angular can use `*ngFor` with `trackBy` for now. If performance becomes an issue, Angular CDK `ScrollingModule` (`cdk-virtual-scroll-viewport`) can be added later.

2. **CSV Export (Task 2.3)** does not need an external library. Use native browser APIs:
   ```typescript
   const csvContent = data.map(row => Object.values(row).join(',')).join('\n');
   const blob = new Blob([csvContent], { type: 'text/csv' });
   const url = URL.createObjectURL(blob);
   // Trigger download via hidden <a> element
   ```

3. **File Upload (Task 3.3)** uses `FormData` with Angular `HttpClient`:
   ```typescript
   const formData = new FormData();
   formData.append('file', file);
   return this.http.post<PreviewData>('/api/import/upload', formData);
   ```
   Angular's `HttpClient` automatically sets the correct `Content-Type` boundary for multipart uploads.

4. **Debounced Search (Tasks 2.1, 2.2)** uses RxJS:
   ```typescript
   searchSubject = new Subject<string>();
   // In ngOnInit:
   this.searchSubject.pipe(debounceTime(500), distinctUntilChanged())
     .subscribe(term => this.fetchData(term));
   ```

5. **The navigation shell already has Sales and Import links** (`main-layout.component.html` lines 6-7), so those will activate as soon as routes are registered.

6. **All new components should be standalone** (matching the existing Angular pattern in this project).
