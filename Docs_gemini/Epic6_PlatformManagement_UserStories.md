# Epic 6: Platform Management - Detailed User Stories

## US-018: View Available Platforms
**As an** author,  
**I want to** see all available publishing platforms in the system,  
**So that** I can select the appropriate platform when adding sales data.

### Acceptance Criteria:
- [ ] A "Platforms" page must be available in the application.
- [ ] The page must display a list of all pre-configured platforms (e.g., Amazon KDP, Draft2Digital, IngramSpark).
- [ ] Each platform in the list should display its name, commission rate, and a link to its website.
- [ ] The user must be able to search for platforms by name.
- [ ] The platform list should be sorted alphabetically by default.

## US-019: Request New Platform
**As an** author,  
**I want to** request a new platform to be added to the system,  
**So that** I can track sales from platforms not currently supported.

### Acceptance Criteria:
- [ ] A "Request New Platform" button or link should be available on the platforms page.
- [ ] The request form must include fields for the platform's name, website, and typical commission rate.
- [ ] Upon submission, the user should receive a confirmation that their request has been received.
- [ ] The submitted requests should be stored in the database for administrative review (the review process itself is a future feature).

## US-020: Unit and Integration Testing
**As a** developer,
**I want to** have comprehensive unit and integration tests for the platform management features,
**So that** I can ensure the reliability of the platform data.

### Acceptance Criteria:
- [ ] Integration tests should cover the `GET /api/platforms` endpoint.
- [ ] Tests should verify that the list of platforms is returned correctly.
- [ ] If a "Request New Platform" endpoint is implemented, it must also be covered by integration tests.
- [ ] All tests should be automated and integrated into the CI/CD pipeline.