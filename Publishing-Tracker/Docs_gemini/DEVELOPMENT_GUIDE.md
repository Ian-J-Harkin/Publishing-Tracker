# Dalenii Publishing Tracker - Development Guide

## 📋 Weekly Implementation Plan

### Week 1: Foundation Setup (Days 1-7)

#### Day 1-2: Project Structure & .NET API
```bash
# Create solution structure
dotnet new sln -n PublishingTracker
dotnet new webapi -n PublishingTracker.Api
dotnet sln add PublishingTracker.Api

# Add required packages
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
```

**Key Files to Create:**
- [`Models/Book.cs`](src/api/Models/Book.cs) - Book entity
- [`Models/Sale.cs`](src/api/Models/Sale.cs) - Sales record entity
- [`Models/Platform.cs`](src/api/Models/Platform.cs) - Publishing platform entity
- [`Data/ApplicationDbContext.cs`](src/api/Data/ApplicationDbContext.cs) - EF Core context
- [`Program.cs`](src/api/Program.cs) - Minimal API configuration

#### Day 3-4: React Frontend Setup
```bash
# Create React TypeScript app
npx create-react-app publishing-tracker-ui --template typescript
cd publishing-tracker-ui

# Install additional packages
npm install react-router-dom @types/react-router-dom
npm install axios react-query
npm install recharts @types/recharts
npm install @mui/material @emotion/react @emotion/styled
```

**Key Files to Create:**
- [`src/types/index.ts`](src/ui/src/types/index.ts) - TypeScript interfaces
- [`src/services/api.ts`](src/ui/src/services/api.ts) - API client setup
- [`src/App.tsx`](src/ui/src/App.tsx) - Main app with routing
- [`src/components/Layout.tsx`](src/ui/src/components/Layout.tsx) - App layout

#### Day 5-7: Basic CRUD Operations
- Database migrations for core entities
- Basic API endpoints for Books and Platforms
- Simple React pages for listing and adding books
- Authentication setup (basic JWT)

### Week 2: Core Business Logic (Days 8-14)

#### Day 8-10: Data Management
**Backend Focus:**
- Complete CRUD operations for all entities
- Sales data endpoints with filtering/sorting
- Dashboard summary endpoints
- Data validation and error handling

**Frontend Focus:**
- Book management page with add/edit forms
- Platform management interface
- Basic data tables with pagination

#### Day 11-14: Dashboard & Visualization
**Backend:**
- Dashboard analytics endpoints
- Sales aggregation queries
- Monthly/yearly reporting logic

**Frontend:**
- Dashboard page with charts (Recharts)
- Revenue tracking charts
- Sales by platform visualization
- Key metrics cards

### Week 3: Advanced Features (Days 15-21)

#### Day 15-17: CSV Import System
**Backend:**
- File upload endpoint
- CSV parsing service
- Background processing simulation
- Import validation and error reporting

**Frontend:**
- File upload component
- Import progress indicator
- Import history page
- Error handling and user feedback

#### Day 18-21: UI Polish & Testing
- Responsive design implementation
- Form validation
- Loading states and error boundaries
- Basic unit tests for critical components
- API integration testing

### Week 4: Azure Deployment (Days 22-28)

#### Day 22-24: Azure Setup
- Create Azure resources (Resource Group, App Service, SQL Database)
- Configure connection strings and app settings
- Set up Azure Key Vault for secrets
- Configure Managed Identity

#### Day 25-26: CI/CD Pipeline
- GitHub Actions workflow setup
- Build and test automation
- Deployment to Azure App Service
- Environment-specific configurations

#### Day 27-28: Final Integration
- Azure Functions for file processing
- Blob Storage integration
- Production testing and debugging
- Documentation and README completion

## 🛠️ Technical Implementation Details

### Entity Models Structure
```csharp
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string ISBN { get; set; }
    public DateTime PublicationDate { get; set; }
    public decimal BasePrice { get; set; }
    public List<Sale> Sales { get; set; }
}

public class Sale
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int PlatformId { get; set; }
    public DateTime SaleDate { get; set; }
    public int Quantity { get; set; }
    public decimal Royalty { get; set; }
    public decimal Revenue { get; set; }
    public Book Book { get; set; }
    public Platform Platform { get; set; }
}
```

### React Component Structure
```typescript
// Main app structure
App.tsx
├── Layout.tsx
├── Dashboard/
│   ├── DashboardPage.tsx
│   ├── MetricsCard.tsx
│   └── SalesChart.tsx
├── Books/
│   ├── BooksPage.tsx
│   ├── BookList.tsx
│   └── BookForm.tsx
└── Sales/
    ├── SalesPage.tsx
    ├── SalesTable.tsx
    └── ImportPage.tsx
```

### API Endpoint Design
```csharp
// Minimal API endpoints in Program.cs
app.MapGet("/api/books", (ApplicationDbContext db) => 
    db.Books.ToListAsync());

app.MapPost("/api/books", (Book book, ApplicationDbContext db) => {
    db.Books.Add(book);
    return db.SaveChangesAsync();
});

app.MapGet("/api/dashboard/summary", (ApplicationDbContext db) => {
    // Return dashboard metrics
});
```

## 🎯 Success Checkpoints

### Week 1 Checkpoint
- [ ] .NET API running locally with Swagger
- [ ] React app displays basic routing
- [ ] Database created with sample data
- [ ] Basic authentication working

### Week 2 Checkpoint
- [ ] All CRUD operations functional
- [ ] Dashboard showing real data
- [ ] Responsive UI components
- [ ] API integration complete

### Week 3 Checkpoint
- [ ] CSV import working end-to-end
- [ ] Professional UI with error handling
- [ ] All core features implemented
- [ ] Local development fully functional

### Week 4 Checkpoint
- [ ] Application deployed to Azure
- [ ] CI/CD pipeline operational
- [ ] All Azure services integrated
- [ ] Production-ready with documentation

## 📚 Learning Resources

### React/TypeScript Quick Start
- [TypeScript in 5 minutes](https://www.typescriptlang.org/docs/handbook/typescript-in-5-minutes.html)
- [React Hooks documentation](https://reactjs.org/docs/hooks-intro.html)
- [Material-UI components](https://mui.com/components/)

### Azure Services
- [Azure App Service quickstart](https://docs.microsoft.com/en-us/azure/app-service/)
- [Azure SQL Database tutorial](https://docs.microsoft.com/en-us/azure/azure-sql/)
- [Azure Functions overview](https://docs.microsoft.com/en-us/azure/azure-functions/)

## 🚀 Deployment Checklist

### Pre-Deployment
- [ ] All features tested locally
- [ ] Environment variables configured
- [ ] Database schema finalized
- [ ] Security review completed

### Azure Resources
- [ ] Resource Group created
- [ ] App Service plan configured
- [ ] Azure SQL Database provisioned
- [ ] Key Vault setup with secrets
- [ ] Storage Account for files

### Production Verification
- [ ] Application accessible via HTTPS
- [ ] Database connectivity verified
- [ ] File upload functionality working
- [ ] Authentication flow tested
- [ ] Performance acceptable

This guide provides a clear path from zero to deployed application while learning React/TypeScript and Azure services incrementally.