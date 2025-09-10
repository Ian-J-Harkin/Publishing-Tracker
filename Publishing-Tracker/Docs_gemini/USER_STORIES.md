# Dalenii Publishing Tracker - User Stories

## Overview
This document contains user stories for the Publishing Tracker application, organized by MVP core features and advanced features. Stories are written from the perspective of independent authors and small publishers managing eBook sales across multiple platforms.

## User Personas

### Primary Persona: Independent Author
- **Name**: Sarah Mitchell
- **Background**: Solo fiction author who self-publishes romance novels
- **Goals**: 
  - Track sales performance across Amazon KDP, Draft2Digital, and Apple Books
  - Understand which books and platforms generate the most revenue
  - Simplify royalty tracking for tax purposes
- **Pain Points**: 
  - Currently uses multiple spreadsheets to track sales from different platforms
  - Difficult to get a consolidated view of performance
  - Time-consuming manual data entry from platform reports
- **Technical Comfort**: Basic to intermediate computer skills, comfortable with web applications

### Secondary Persona: Small Publisher
- **Name**: Marcus Thompson (Dalenii Digital)
- **Background**: Small publishing house with 15 authors and 50+ active titles
- **Goals**:
  - Monitor portfolio performance across all titles and authors
  - Track royalty payments to authors
  - Identify trending genres and successful platforms
  - Make data-driven decisions about marketing spend
- **Pain Points**:
  - Complex data aggregation from multiple sources
  - Need to generate reports for authors and stakeholders
  - Difficulty identifying underperforming titles
- **Technical Comfort**: Advanced computer skills, uses various business tools

## Core MVP User Stories

### Epic 1: Authentication & User Management

#### US-001: User Registration
**As an** independent author  
**I want to** create an account with my email and password  
**So that** I can securely access my publishing data  

**Acceptance Criteria:**
- [ ] User can register with email, first name, last name, and password
- [ ] Email addresses must be unique in the system
- [ ] Password must meet security requirements (minimum 8 characters, include uppercase, lowercase, number)
- [ ] User receives confirmation of successful registration
- [ ] Invalid data shows appropriate error messages
- [ ] User is automatically logged in after successful registration

**Priority:** High  
**Story Points:** 3  
**Dependencies:** None

#### US-002: User Login
**As a** registered user  
**I want to** log in with my email and password  
**So that** I can access my publishing dashboard and data  

**Acceptance Criteria:**
- [ ] User can log in with valid email and password combination
- [ ] Invalid credentials show appropriate error message
- [ ] Successful login redirects to dashboard
- [ ] Login session persists until user logs out or session expires
- [ ] "Remember Me" option keeps user logged in longer

**Priority:** High  
**Story Points:** 2  
**Dependencies:** US-001

#### US-003: User Logout
**As a** logged-in user  
**I want to** securely log out of my account  
**So that** my data remains protected when I'm done using the application  

**Acceptance Criteria:**
- [ ] User can log out from any page in the application
- [ ] Logout clears the user session completely
- [ ] After logout, user is redirected to login page
- [ ] Attempting to access protected pages after logout redirects to login

**Priority:** Medium  
**Story Points:** 1  
**Dependencies:** US-002

### Epic 2: Book Management

#### US-004: View My Books
**As an** author  
**I want to** see a list of all my published books  
**So that** I can quickly access and manage my book portfolio  

**Acceptance Criteria:**
- [ ] User sees a list of all their books on the books page
- [ ] Each book displays title, author, publication date, and genre
- [ ] Books are sorted by publication date (newest first) by default
- [ ] User can search books by title or author
- [ ] Empty state shows helpful message when no books exist
- [ ] List is paginated if user has more than 20 books

**Priority:** High  
**Story Points:** 3  
**Dependencies:** US-002

#### US-005: Add New Book
**As an** author  
**I want to** add a new book to my portfolio  
**So that** I can start tracking its sales performance  

**Acceptance Criteria:**
- [ ] User can access "Add Book" functionality from the books list
- [ ] Form includes fields: Title (required), Author (required), ISBN, Publication Date, Base Price, Genre, Description
- [ ] Form validates required fields before submission
- [ ] Successful submission adds book to user's list and shows confirmation
- [ ] User is redirected to the books list after successful addition
- [ ] Form shows appropriate error messages for invalid data

**Priority:** High  
**Story Points:** 3  
**Dependencies:** US-004

#### US-006: Edit Book Details
**As an** author  
**I want to** edit my book information  
**So that** I can keep my book details accurate and up-to-date  

**Acceptance Criteria:**
- [ ] User can access edit functionality from the book list or book detail view
- [ ] Edit form is pre-populated with current book data
- [ ] User can modify any book field except creation date
- [ ] Changes are saved immediately when form is submitted
- [ ] User sees confirmation message after successful update
- [ ] Cancel option returns user to previous view without saving changes

**Priority:** Medium  
**Story Points:** 2  
**Dependencies:** US-005

#### US-007: Delete Book
**As an** author  
**I want to** remove a book from my portfolio  
**So that** I can clean up books that are no longer relevant  

**Acceptance Criteria:**
- [ ] User can access delete functionality from book list or detail view
- [ ] System shows confirmation dialog before deletion
- [ ] Deletion removes book and all associated sales data
- [ ] User sees confirmation message after successful deletion
- [ ] Deleted books cannot be recovered (warning shown in confirmation)
- [ ] User is returned to books list after deletion

**Priority:** Low  
**Story Points:** 2  
**Dependencies:** US-006

### Epic 3: Sales Data Management

#### US-008: View Sales Overview
**As an** author  
**I want to** see a summary of my recent sales activity  
**So that** I can quickly understand my current performance  

**Acceptance Criteria:**
- [ ] User sees total sales for current month, previous month, and last 6 months
- [ ] Display shows total revenue, total units sold, and average royalty per sale
- [ ] Data is grouped by time period with clear labels
- [ ] Loading state is shown while data is being fetched
- [ ] Empty state is shown when no sales data exists
- [ ] Numbers are formatted appropriately (currency, percentages)

**Priority:** High  
**Story Points:** 5  
**Dependencies:** US-002

#### US-009: View Detailed Sales List
**As an** author  
**I want to** see a detailed list of all my sales transactions  
**So that** I can analyze individual sales and identify patterns  

**Acceptance Criteria:**
- [ ] User sees a table with all sales: date, book title, platform, quantity, unit price, royalty, revenue
- [ ] Sales are sorted by date (newest first) by default
- [ ] User can sort by any column (date, book, platform, revenue)
- [ ] User can filter by date range, book, or platform
- [ ] Table shows 25 sales per page with pagination
- [ ] Each sale links to book details page

**Priority:** High  
**Story Points:** 4  
**Dependencies:** US-008

#### US-010: Add Manual Sale Entry
**As an** author  
**I want to** manually add individual sales records  
**So that** I can track sales from platforms that don't provide CSV exports  

**Acceptance Criteria:**
- [ ] User can access "Add Sale" from sales list page
- [ ] Form includes: Book (dropdown), Platform (dropdown), Sale Date, Quantity, Unit Price, Royalty
- [ ] System automatically calculates total revenue (quantity × royalty)
- [ ] Form validates all required fields and date formats
- [ ] Successful submission adds sale and shows confirmation
- [ ] User can add multiple sales in succession

**Priority:** Medium  
**Story Points:** 3  
**Dependencies:** US-009

### Epic 4: Dashboard & Analytics (MVP)

#### US-011: View Dashboard Overview
**As an** author
**I want to** see a high-level dashboard of my publishing performance
**So that** I can quickly understand my business at a glance

**Acceptance Criteria:**
- [ ] Dashboard shows total revenue for current month, previous month, and last 6 months
- [ ] Display shows total books published and total sales transactions
- [ ] Shows top performing book by revenue for the current month
- [ ] Shows top performing platform by revenue for the current month
- [ ] All metrics have appropriate loading states
- [ ] Dashboard is the first page user sees after login

**Priority:** High
**Story Points:** 5
**Dependencies:** US-008, US-009

#### US-012: View Revenue Chart
**As an** author
**I want to** see a visual chart of my revenue over time
**So that** I can identify trends and patterns in my sales

**Acceptance Criteria:**
- [ ] Chart displays monthly revenue for the last 6 months
- [ ] Chart is interactive with hover tooltips showing exact values
- [ ] Chart shows a clear trend line or bar chart visualization
- [ ] User can toggle between 3 months, 6 months, and 1 year views
- [ ] Chart gracefully handles months with no sales (shows zero)
- [ ] Chart is responsive and works on mobile devices

**Priority:** Medium
**Story Points:** 4
**Dependencies:** US-011

#### US-013: View Platform Performance Summary
**As an** author
**I want to** see how my books perform on different platforms
**So that** I can focus my efforts on the most profitable platforms

**Acceptance Criteria:**
- [ ] Shows revenue breakdown by platform for the last 6 months
- [ ] Displays platform names with total revenue and percentage of total
- [ ] Shows number of sales transactions per platform
- [ ] Data is presented in both table and chart format
- [ ] User can filter by date range (1 month, 3 months, 6 months, 1 year)
- [ ] Empty state shows when no sales data exists

**Priority:** Medium
**Story Points:** 3
**Dependencies:** US-012

### Epic 5: CSV Import (Basic)

#### US-014: Upload CSV File
**As an** author
**I want to** upload a CSV file containing my sales data
**So that** I can quickly import large amounts of sales data from platform reports

**Acceptance Criteria:**
- [ ] User can select and upload CSV files from their computer
- [ ] System accepts common CSV formats (.csv, .txt)
- [ ] File size is limited to 5MB maximum
- [ ] Upload shows progress indicator during file transfer
- [ ] User sees confirmation when file is successfully uploaded
- [ ] Appropriate error messages for invalid file types or sizes

**Priority:** High
**Story Points:** 4
**Dependencies:** US-010

#### US-015: Map CSV Columns
**As an** author
**I want to** map CSV columns to the correct data fields
**So that** the system can correctly import my sales data regardless of CSV format

**Acceptance Criteria:**
- [ ] System shows preview of CSV data (first 5 rows)
- [ ] User can map CSV columns to: Book Title, Platform, Sale Date, Quantity, Unit Price, Royalty
- [ ] System auto-detects common column names and suggests mappings
- [ ] User can skip columns that are not needed
- [ ] Mapping configuration is saved for future imports from the same platform
- [ ] Clear validation messages for required field mappings

**Priority:** High
**Story Points:** 5
**Dependencies:** US-014

#### US-016: Process CSV Import
**As an** author
**I want to** process my mapped CSV data and import it into the system
**So that** I can see my sales data in the dashboard and reports

**Acceptance Criteria:**
- [ ] System processes CSV data and creates sales records
- [ ] Shows progress indicator during processing
- [ ] Automatically matches book titles to existing books in user's library
- [ ] Creates new books automatically if titles don't exist
- [ ] Shows summary of import results: records processed, successful, failed
- [ ] Displays any error messages for failed records
- [ ] User can review and fix failed records before re-importing

**Priority:** High
**Story Points:** 6
**Dependencies:** US-015

#### US-017: View Import History
**As an** author
**I want to** see a history of my CSV imports
**So that** I can track what data has been imported and troubleshoot any issues

**Acceptance Criteria:**
- [ ] Shows list of all import jobs with filename, date, and status
- [ ] Displays import statistics: total records, successful, failed
- [ ] User can view detailed error logs for failed imports
- [ ] User can re-download processed files for reference
- [ ] Import history is paginated and searchable
- [ ] Failed imports can be retried with corrected data

**Priority:** Low
**Story Points:** 3
**Dependencies:** US-016

### Epic 6: Platform Management

#### US-018: View Available Platforms
**As an** author
**I want to** see all available publishing platforms in the system
**So that** I can select the appropriate platform when adding sales data

**Acceptance Criteria:**
- [ ] System displays list of pre-configured platforms (Amazon KDP, Draft2Digital, IngramSpark, Apple Books, etc.)
- [ ] Each platform shows name, commission rate, and contact information
- [ ] User can search platforms by name
- [ ] Platform list is sorted alphabetically by default
- [ ] Inactive platforms are clearly marked or hidden
- [ ] Platform details include typical CSV format information

**Priority:** Medium
**Story Points:** 2
**Dependencies:** None

#### US-019: Request New Platform
**As an** author
**I want to** request a new platform to be added to the system
**So that** I can track sales from platforms not currently supported

**Acceptance Criteria:**
- [ ] User can submit a request form with platform name, website, and contact details
- [ ] Form includes field for typical commission rate and CSV format
- [ ] User receives confirmation that request has been submitted
- [ ] Requests are stored for admin review (future feature)
- [ ] User can see status of their platform requests
- [ ] Form validates required fields and email formats

**Priority:** Low
**Story Points:** 3
**Dependencies:** US-018

---

## Advanced User Stories

### Epic 7: Enhanced Dashboard Features

#### US-020: Advanced Analytics Dashboard
**As an** author
**I want to** see detailed analytics about my publishing performance
**So that** I can make data-driven decisions about my publishing strategy

**Acceptance Criteria:**
- [ ] Dashboard shows revenue trends with year-over-year comparison
- [ ] Displays seasonal performance patterns (quarterly breakdown)
- [ ] Shows average revenue per book and per platform
- [ ] Includes conversion metrics (sales vs marketing spend if available)
- [ ] User can customize dashboard layout and metrics
- [ ] Dashboard auto-refreshes with latest data

**Priority:** Low
**Story Points:** 8
**Dependencies:** US-013

#### US-021: Book Performance Analytics
**As an** author
**I want to** see detailed performance analytics for each book
**So that** I can understand which books are most successful and why

**Acceptance Criteria:**
- [ ] Shows individual book performance charts (sales over time)
- [ ] Displays platform breakdown for each book
- [ ] Compares book performance against user's average
- [ ] Shows publication date correlation with initial sales
- [ ] Includes genre performance comparisons
- [ ] User can export book performance reports

**Priority:** Low
**Story Points:** 6
**Dependencies:** US-020

### Epic 8: Advanced Import Features

#### US-022: Background Processing
**As an** author
**I want to** import large CSV files without waiting for processing to complete
**So that** I can continue using the application while imports run in the background

**Acceptance Criteria:**
- [ ] Large imports (>1000 records) process in the background
- [ ] User receives email notification when import completes
- [ ] Import status is visible on dashboard and import history
- [ ] User can cancel long-running imports
- [ ] System handles multiple concurrent imports per user
- [ ] Background jobs resume after system restarts

**Priority:** Low
**Story Points:** 8
**Dependencies:** US-017

#### US-023: Smart Data Validation
**As an** author
**I want to** have my CSV data validated and cleaned automatically
**So that** I can import data with confidence in its accuracy

**Acceptance Criteria:**
- [ ] System detects and flags duplicate sales records
- [ ] Validates date formats and converts automatically
- [ ] Checks for reasonable price and royalty amounts
- [ ] Suggests corrections for common book title variations
- [ ] Flags unusual sales patterns for review
- [ ] User can configure validation rules and thresholds

**Priority:** Low
**Story Points:** 7
**Dependencies:** US-022

### Epic 9: User Profile & Account Management

#### US-024: Edit Profile Information
**As a** registered user
**I want to** update my profile information
**So that** I can keep my account details current

**Acceptance Criteria:**
- [ ] User can edit first name, last name, and email address
- [ ] Email changes require confirmation via new email address
- [ ] Form validates email format and uniqueness
- [ ] User sees confirmation message after successful update
- [ ] Changes are reflected immediately throughout the application
- [ ] User can upload profile picture (optional)

**Priority:** Low
**Story Points:** 3
**Dependencies:** US-003

#### US-025: Change Password
**As a** registered user
**I want to** change my password
**So that** I can maintain account security

**Acceptance Criteria:**
- [ ] User must enter current password to change to new password
- [ ] New password must meet security requirements
- [ ] Password change logs user out of all other sessions
- [ ] User receives email confirmation of password change
- [ ] Form shows password strength indicator
- [ ] Clear error messages for incorrect current password

**Priority:** Medium
**Story Points:** 3
**Dependencies:** US-024

### Epic 10: Mobile & Responsive Design

#### US-026: Mobile Dashboard Experience
**As an** author using a mobile device
**I want to** access my publishing dashboard on my phone
**So that** I can check my sales performance while away from my computer

**Acceptance Criteria:**
- [ ] Dashboard layout adapts to mobile screen sizes (320px minimum)
- [ ] Charts and graphs are touch-friendly and readable on mobile
- [ ] Navigation menu collapses appropriately for mobile
- [ ] Text remains legible without horizontal scrolling
- [ ] Touch targets are appropriately sized (minimum 44px)
- [ ] Page load times are optimized for mobile networks

**Priority:** Medium
**Story Points:** 5
**Dependencies:** US-011, US-012

#### US-027: Mobile Data Entry
**As an** author using a mobile device
**I want to** add books and sales data from my phone
**So that** I can capture information immediately when I receive sales notifications

**Acceptance Criteria:**
- [ ] Forms are optimized for mobile input (appropriate keyboards, field sizes)
- [ ] Form validation works properly on mobile browsers
- [ ] Success/error messages are clearly visible on mobile
- [ ] Multi-step forms have clear progress indicators
- [ ] File upload works on mobile devices
- [ ] Auto-save functionality prevents data loss

**Priority:** Medium
**Story Points:** 4
**Dependencies:** US-005, US-010, US-026

---

## User Story Mapping & Development Phases

### Development Phases

#### Phase 1 - MVP Foundation (Week 1)
**Goal**: Basic application with authentication and core CRUD operations

**Sprint 1.1 - Authentication & Setup**
- US-001: User Registration
- US-002: User Login
- US-003: User Logout
- US-018: View Available Platforms

**Sprint 1.2 - Book Management**
- US-004: View My Books
- US-005: Add New Book

#### Phase 2 - Core Functionality (Week 2)
**Goal**: Complete sales tracking and basic dashboard

**Sprint 2.1 - Sales Management**
- US-006: Edit Book Details
- US-008: View Sales Overview
- US-009: View Detailed Sales List
- US-010: Add Manual Sale Entry

**Sprint 2.2 - Dashboard**
- US-011: View Dashboard Overview
- US-012: View Revenue Chart

#### Phase 3 - Data Import & Analytics (Week 3)
**Goal**: CSV import functionality and enhanced reporting

**Sprint 3.1 - CSV Import**
- US-014: Upload CSV File
- US-015: Map CSV Columns
- US-016: Process CSV Import

**Sprint 3.2 - Analytics & Reports**
- US-013: View Platform Performance Summary
- US-017: View Import History

#### Phase 4 - Polish & Advanced Features (Week 4)
**Goal**: User experience improvements and deployment preparation

**Sprint 4.1 - User Management**
- US-024: Edit Profile Information
- US-025: Change Password
- US-007: Delete Book

**Sprint 4.2 - Mobile & Final Polish**
- US-026: Mobile Dashboard Experience
- US-027: Mobile Data Entry
- US-019: Request New Platform

#### Future Enhancements (Post-MVP)
**Advanced Analytics**
- US-020: Advanced Analytics Dashboard
- US-021: Book Performance Analytics

**Advanced Import Features**
- US-022: Background Processing
- US-023: Smart Data Validation

---

## Story Point Summary

### By Priority
- **High Priority**: 35 story points (7 stories)
- **Medium Priority**: 28 story points (8 stories)
- **Low Priority**: 28 story points (10 stories)

### By Epic
- **Epic 1 (Authentication)**: 6 story points
- **Epic 2 (Book Management)**: 10 story points
- **Epic 3 (Sales Management)**: 12 story points
- **Epic 4 (Dashboard/Analytics)**: 12 story points
- **Epic 5 (CSV Import)**: 18 story points
- **Epic 6 (Platform Management)**: 5 story points
- **Epic 7 (Advanced Analytics)**: 14 story points
- **Epic 8 (Advanced Import)**: 15 story points
- **Epic 9 (User Profile)**: 6 story points
- **Epic 10 (Mobile/Responsive)**: 9 story points

### MVP Total: 63 story points (Phases 1-4)
### Advanced Features: 28 story points

---

## Success Metrics

### User Engagement
- Time to first successful data import < 10 minutes
- Dashboard load time < 3 seconds
- User completes account setup within first session
- Mobile usage accounts for >30% of sessions

### Data Quality
- CSV import success rate > 95%
- Data validation accuracy > 98%
- Zero data loss during imports
- Form completion rate > 85%

### User Experience
- Mobile responsiveness score > 90% (Google PageSpeed)
- User task completion rate > 90%
- Average session duration > 5 minutes
- User returns within 7 days > 60%

### Technical Performance
- API response times < 500ms for all endpoints
- Support for concurrent users (minimum 100)
- Uptime > 99.5% in production
- Mobile page load time < 3 seconds

---

*This completes the comprehensive user stories for the Publishing Tracker MVP and advanced features. Each story includes detailed acceptance criteria, priority levels, story point estimates, and dependencies to guide development planning and implementation.*

---
**Post-MVP Review Note:**
The initial MVP implementation of authentication will use password hashing with BCrypt. A post-MVP task should be created to review and enhance the security posture, which may include:
- Implementing multi-factor authentication (MFA).
- Adding more robust password policies.
- Conducting a security audit of the authentication and authorization mechanisms.
---
