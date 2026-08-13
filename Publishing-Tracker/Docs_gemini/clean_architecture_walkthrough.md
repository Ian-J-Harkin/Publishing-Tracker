# Clean Architecture & DDD Walkthrough: Refactoring `BookService`

The original [architectural_review.md](file:///c:/Github/PublishingTracker/Publishing-Tracker/Docs_gemini/architectural_review.md) highlighted several core issues with the current backend architecture: a lack of layer separation, anemic domain models, "fat" services, and infrastructure leakage. 

This document expands on those findings by providing a concrete, step-by-step walkthrough of how we would refactor the `BookService` and the `Book` entity to strictly adhere to **Clean Architecture**, **Domain-Driven Design (DDD)**, and **SOLID** principles.

---

## 1. The Current State (The Problem)

Currently, [BookService.cs](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Services/BookService.cs) is a "Fat Service". It handles:
- Authorization (`book.UserId != _currentUserService.UserId`)
- Business Logic (Orchestrating creation and updates)
- Data Access and Infrastructure leakage (`_saleRepository.Query().Include(...)`)
- DTO Mapping

Conversely, the [Book.cs](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Models/Book.cs) entity is "Anemic". It is merely a data bag with public getters and setters. It enforces no business rules and guarantees no invariants.

> [!WARNING]
> This violates the **Single Responsibility Principle (SRP)** (the service does too much) and the **Dependency Inversion Principle (DIP)** (the application logic is tightly coupled to Entity Framework concepts like `Include`).

---

## 2. Step 1: Physical Folder Reorganization

Before changing any code, the first step to achieving Clean Architecture is establishing the physical boundaries (layers) via folders (or separate C# Class Library projects). This enforces the **Dependency Rule**: inner layers (Domain) must not depend on outer layers (Infrastructure/Presentation).

Assuming we are keeping everything inside a single project (`PublishingTracker.Api`), we create the following root-level folders to replace generic folders like `Models` and `Services`:

```text
PublishingTracker.Api/
├── Domain/                         <-- Core entities and rules. NO external dependencies.
│   └── Entities/
│       └── Book.cs                 (Moved from Models)
├── Application/                    <-- Use Cases and Interfaces (Ports). Depends ONLY on Domain.
│   ├── Interfaces/
│   │   └── IBookRepository.cs      
│   └── Books/
│       └── Commands/
│           └── CreateBookCommand.cs
├── Infrastructure/                 <-- Database, Email, external APIs (Adapters). Depends on Application.
│   └── Persistence/
│       ├── PublishingTrackerDbContext.cs (Moved from Data)
│       └── Repositories/
│           └── BookRepository.cs   
└── Presentation/                   <-- Minimal APIs, Controllers. (Often just kept at the root in a single project)
```

---

## 3. Step 2: Rich Domain Model (DDD)

With `Book.cs` relocated to the `Domain/Entities` folder, we must ensure it protects its invariants. We transition `Book` from a data bag to a Rich Domain Model.

**Changes Required:**
1. **Private Setters:** Properties should not be arbitrarily mutable from the outside.
2. **Constructors:** Enforce required data upon creation (a Book must always have a Title and a UserId).
3. **Domain Methods:** State changes happen through explicit methods that carry business meaning (e.g., `UpdatePricing`, `CorrectIsbn`), rather than setting properties directly.

```csharp
// Domain/Entities/Book.cs
public class Book : IEntity // Or perhaps an AggregateRoot base class
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Title { get; private set; }
    public string Author { get; private set; }
    public string? ISBN { get; private set; }
    public decimal? BasePrice { get; private set; }
    // ... other properties

    // Private constructor for EF Core
    private Book() { }

    // Factory method or public constructor enforcing invariants
    public Book(int userId, string title, string author)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required");
        
        UserId = userId;
        Title = title;
        Author = author;
    }

    // Domain behavior
    public void UpdatePricing(decimal newPrice)
    {
        if (newPrice < 0) throw new InvalidDomainStateException("Price cannot be negative.");
        BasePrice = newPrice;
    }

    public void VerifyOwnership(int currentUserId)
    {
        if (UserId != currentUserId)
            throw new UnauthorizedAccessException("User does not own this book.");
    }
}
```

---

## 4. Step 3: Defining Ports (Application Layer)

Clean architecture dictates that the Application core defines the interfaces (Ports) it needs to talk to the outside world, and the Infrastructure layer implements them (Adapters). 

Instead of a generic `IRepository<T>`, we define a specific `IBookRepository` tailored to the Use Cases. This interface belongs in `Application/Interfaces/`.

```csharp
// Application/Interfaces/IBookRepository.cs
public interface IBookRepository
{
    Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Book book, CancellationToken cancellationToken);
    Task UpdateAsync(Book book, CancellationToken cancellationToken);
    Task DeleteAsync(Book book, CancellationToken cancellationToken);
    
    // Read-only queries can return specialized DTOs directly to bypass the domain model for performance
    Task<List<BookPerformanceDto>> GetBookPerformanceAsync(int bookId, CancellationToken cancellationToken);
}
```

---

## 5. Step 4: Moving from Fat Service to CQRS (Application Layer)

Instead of a monolithic `BookService` with `Create`, `Update`, `Delete`, `GetById`, and `GetPerformance`, we split these into individual **Commands** and **Queries** (CQRS) within the `Application` folder. This perfectly aligns with the **Single Responsibility Principle (SRP)**.

Let's look at how the `CreateAsync` method from `BookService` becomes a MediatR Command Handler.

### The Command
```csharp
// Application/Books/Commands/CreateBookCommand.cs
public record CreateBookCommand(string Title, string Author, string ISBN) : IRequest<Result<int>>;
```

### The Handler
```csharp
// Application/Books/Commands/CreateBookCommandHandler.cs
public class CreateBookCommandHandler : IRequestHandler<CreateBookCommand, Result<int>>
{
    private readonly IBookRepository _bookRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateBookCommandHandler(IBookRepository bookRepository, ICurrentUserService currentUserService)
    {
        _bookRepository = bookRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> Handle(CreateBookCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result<int>.Failure("Unauthorized");

        // 1. Create using Domain logic
        var book = new Book(userId.Value, request.Title, request.Author);
        
        // Optional: add other properties via domain methods
        // book.SetIsbn(request.ISBN);

        // 2. Persist using the Port interface (Dependency Inversion)
        await _bookRepository.AddAsync(book, cancellationToken);

        return Result<int>.Success(book.Id);
    }
}
```

By isolating this into a single handler, if we need to change how books are created, we only touch this file. It is **Closed for modification but Open for extension (OCP)**.

---

## 6. Step 5: Infrastructure Adapters (Infrastructure Layer)

The actual EF Core logic, including `Include()`, `ToListAsync()`, and DbContext dependencies, is pushed entirely to the `Infrastructure/Persistence/` folder. The core application knows nothing about Entity Framework.

```csharp
// Infrastructure/Persistence/Repositories/BookRepository.cs
public class BookRepository : IBookRepository
{
    private readonly PublishingTrackerDbContext _context;

    public BookRepository(PublishingTrackerDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        await _context.Books.AddAsync(book, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<BookPerformanceDto>> GetBookPerformanceAsync(int bookId, CancellationToken cancellationToken)
    {
        // Infrastructure leakage is safely contained here!
        return await _context.Sales
            .Include(s => s.Platform)
            .Where(s => s.BookId == bookId)
            .GroupBy(s => new { s.Platform.Name, s.Currency })
            .Select(g => new BookPerformanceDto 
            { 
                PlatformName = g.Key.Name,
                Currency = g.Key.Currency,
                TotalRevenue = g.Sum(s => s.Revenue) 
            })
            .ToListAsync(cancellationToken);
    }
    // ... other methods
}
```

---

## Summary of SOLID & DDD Gains

By dismantling `BookService` and spreading its responsibilities across the appropriate architectural layers:

1. **Single Responsibility Principle (SRP):** Each Command/Query handler does exactly one thing. The `Book` entity is solely responsible for its own state.
2. **Open/Closed Principle (OCP):** New features (e.g., `PublishBookCommand`) can be added by creating new handler classes without modifying existing services.
3. **Liskov Substitution Principle (LSP) & Interface Segregation Principle (ISP):** Instead of a generic `IRepository<T>` that forces unimplemented methods, we use a tailored `IBookRepository`.
4. **Dependency Inversion Principle (DIP):** The application layer depends on `IBookRepository` (abstraction). EF Core logic depends on `IBookRepository` (abstraction). The core is free of database coupling.
5. **Domain-Driven Design:** The `Book` entity is now a rich model. Business rules (like "a price cannot be negative") are enforced at the core domain level, making invalid states unrepresentable.
