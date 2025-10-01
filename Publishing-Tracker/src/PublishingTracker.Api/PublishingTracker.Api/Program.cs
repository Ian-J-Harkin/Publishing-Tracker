using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(allowedOrigins!)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

if (IsRunningInAzure())
{
    Console.WriteLine("Running in Azure environment.");
    builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("NeonConnection")));
}
else
{
    Console.WriteLine("Running in Local/Development environment.");
    // Use PostgreSQL for local development
    builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        var jwtAudience = builder.Configuration["Jwt:Audience"];
        var jwtKey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience)) {
            throw new InvalidOperationException("JWT configuration is incomplete.");
        }
        
    });


builder.Services.AddAuthorization();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();

    Console.WriteLine($"Creating database scope");
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
            // wrap in try-catch to handle potential migration issues
            try
            {
                db.Database.Migrate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration failed: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database setup failed: {ex.Message}");
    }
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/api/auth/register", async (IAuthService authService, RegisterRequest request) =>
{
    var authResponse = await authService.RegisterAsync(request);
    return authResponse == null ? Results.BadRequest("Email already exists.") : Results.Ok(authResponse);
});

app.MapPost("/api/auth/login", async (IAuthService authService, LoginRequest request) =>
{
    var authResponse = await authService.LoginAsync(request);
    if (authResponse == null)
        {
            return Results.Unauthorized();
        }
    return authResponse == null ? Results.Unauthorized() : Results.Ok(authResponse);
});

var booksGroup = app.MapGroup("/api/books").RequireAuthorization();

booksGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db) =>
{
    var books = await db.Books.ToListAsync();
    return Results.Ok(books);
});

booksGroup.MapGet("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id) =>
{
    var book = await db.Books.FindAsync(id);
    return book == null ? Results.NotFound() : Results.Ok(book);
});

booksGroup.MapPost("/", async ([FromServices] PublishingTrackerDbContext db, CreateBookDto createBookDto, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

    var book = new Book
    {
        Title = createBookDto.Title,
        Author = createBookDto.Author,
        ISBN = createBookDto.ISBN,
        PublicationDate = createBookDto.PublicationDate,
        BasePrice = createBookDto.BasePrice,
        Genre = createBookDto.Genre,
        Description = createBookDto.Description,
        UserId = userId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.Books.Add(book);
    await db.SaveChangesAsync();

    return Results.Created($"/api/books/{book.Id}", book);
});

booksGroup.MapPut("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id, UpdateBookDto updateBookDto) =>
{
    var book = await db.Books.FindAsync(id);
    if (book == null)
    {
        return Results.NotFound();
    }

    if (updateBookDto.Title != null) book.Title = updateBookDto.Title;
    if (updateBookDto.Author != null) book.Author = updateBookDto.Author;
    if (updateBookDto.ISBN != null) book.ISBN = updateBookDto.ISBN;
    if (updateBookDto.PublicationDate.HasValue) book.PublicationDate = updateBookDto.PublicationDate;
    if (updateBookDto.BasePrice.HasValue) book.BasePrice = updateBookDto.BasePrice;
    if (updateBookDto.Genre != null) book.Genre = updateBookDto.Genre;
    if (updateBookDto.Description != null) book.Description = updateBookDto.Description;
    book.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(book);
});

booksGroup.MapDelete("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id) =>
{
    var book = await db.Books.FindAsync(id);
    if (book == null)
    {
        return Results.NotFound();
    }

    db.Books.Remove(book);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

booksGroup.MapGet("/{id}/performance", async ([FromServices] PublishingTrackerDbContext db, int id, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

    var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
    if (book == null)
    {
        return Results.NotFound("Book not found or does not belong to the user.");
    }

    var performanceData = await db.Sales
        .Where(s => s.BookId == id)
        .GroupBy(s => s.Platform.Name)
        .Select(g => new { PlatformName = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
        .ToListAsync();

    return Results.Ok(performanceData);
});

var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();

salesGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
    var sales = await db.Sales
        .Where(s => s.Book.UserId == userId)
        .Select(s => new SaleDto
        {
            Id = s.Id,
            BookId = s.BookId,
            BookTitle = s.Book.Title,
            PlatformId = s.PlatformId,
            PlatformName = s.Platform.Name,
            SaleDate = s.SaleDate,
            Quantity = s.Quantity,
            UnitPrice = s.UnitPrice,
            Royalty = s.Royalty,
            Revenue = s.Revenue
        })
        .ToListAsync();
    return Results.Ok(sales);
});

salesGroup.MapPost("/", async ([FromServices] PublishingTrackerDbContext db, CreateSaleDto createSaleDto, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
    var book = await db.Books.FirstOrDefaultAsync(b => b.Id == createSaleDto.BookId && b.UserId == userId);
    if (book == null)
    {
        return Results.NotFound("Book not found or does not belong to the user.");
    }

    var sale = new Sale
    {
        BookId = createSaleDto.BookId,
        PlatformId = createSaleDto.PlatformId,
        SaleDate = createSaleDto.SaleDate,
        Quantity = createSaleDto.Quantity,
        UnitPrice = createSaleDto.UnitPrice,
        Royalty = createSaleDto.Royalty,
        Revenue = createSaleDto.Quantity * createSaleDto.Royalty,
        CreatedAt = DateTime.UtcNow
    };

    db.Sales.Add(sale);
    await db.SaveChangesAsync();

    return Results.Created($"/api/sales/{sale.Id}", sale);
});

var platformsGroup = app.MapGroup("/api/platforms").RequireAuthorization();

platformsGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db) =>
{
    var platforms = await db.Platforms
        .Select(p => new PlatformDto
        {
            Id = p.Id,
            Name = p.Name,
            BaseUrl = p.BaseUrl!,
            CommissionRate = p.CommissionRate ?? 0
        })
        .ToListAsync();
    return Results.Ok(platforms);
});

platformsGroup.MapPost("/requests", async ([FromServices] PublishingTrackerDbContext db, PlatformRequestDto requestDto, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

    var platformRequest = new PlatformRequest
    {
        Name = requestDto.Name,
        BaseUrl = requestDto.BaseUrl,
        CommissionRate = requestDto.CommissionRate,
        UserId = userId
    };

    db.PlatformRequests.Add(platformRequest);
    await db.SaveChangesAsync();

    return Results.Created($"/api/platforms/requests/{platformRequest.Id}", platformRequest);
})
.WithName("RequestPlatform")
.WithOpenApi();

var dashboardGroup = app.MapGroup("/api/dashboard").RequireAuthorization();

dashboardGroup.MapGet("/summary", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

    var totalRevenue = await db.Sales
        .Where(s => s.Book.UserId == userId)
        .SumAsync(s => s.Revenue);

    var totalBooksPublished = await db.Books
        .CountAsync(b => b.UserId == userId);

    var totalSalesTransactions = await db.Sales
        .CountAsync(s => s.Book.UserId == userId);

    var topPerformingBook = await db.Sales
        .Where(s => s.Book.UserId == userId)
        .GroupBy(s => s.Book.Title)
        .Select(g => new { Title = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
        .OrderByDescending(x => x.TotalRevenue)
        .FirstOrDefaultAsync();

    var topPerformingPlatform = await db.Sales
        .Where(s => s.Book.UserId == userId)
        .GroupBy(s => s.Platform.Name)
        .Select(g => new { PlatformName = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
        .OrderByDescending(x => x.TotalRevenue)
        .FirstOrDefaultAsync();

    var summary = new DashboardSummaryDto
    {
        TotalRevenue = totalRevenue,
        TotalBooksPublished = totalBooksPublished,
        TotalSalesTransactions = totalSalesTransactions,
        TopPerformingBook = topPerformingBook?.Title ?? "N/A",
        TopPerformingPlatform = topPerformingPlatform?.PlatformName ?? "N/A"
    };

    return Results.Ok(summary);
});

dashboardGroup.MapGet("/yoy", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

    var currentYearRevenue = await db.Sales
        .Where(s => s.Book.UserId == userId && s.SaleDate.Year == DateTime.Now.Year)
        .SumAsync(s => s.Revenue);

    var previousYearRevenue = await db.Sales
        .Where(s => s.Book.UserId == userId && s.SaleDate.Year == DateTime.Now.Year - 1)
        .SumAsync(s => s.Revenue);

    var yoyComparison = new
    {
        CurrentYearRevenue = currentYearRevenue,
        PreviousYearRevenue = previousYearRevenue,
        Growth = previousYearRevenue > 0 ? (currentYearRevenue - previousYearRevenue) / previousYearRevenue : 0
    };

    return Results.Ok(yoyComparison);
});

dashboardGroup.MapGet("/seasonal", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

    var seasonalData = await db.Sales
        .Where(s => s.Book.UserId == userId)
        .GroupBy(s => s.SaleDate.Month)
        .Select(g => new { Month = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
        .OrderBy(x => x.Month)
        .ToListAsync();

    return Results.Ok(seasonalData);
});

var importGroup = app.MapGroup("/api/import").RequireAuthorization();

importGroup.MapPost("/upload", async ([FromServices] PublishingTrackerDbContext db, IFormFile file, HttpContext httpContext) =>
{
    // This is a placeholder for the upload logic.
    // In a real application, you would save the file and return a file ID.
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream); // Example async operation

    // Save to DB or storage here...
    return Results.Ok(new { fileName = file.FileName });
});

importGroup.MapPost("/process", async ([FromServices] PublishingTrackerDbContext db, ColumnMappingDto mapping, HttpContext httpContext) =>
{
    // This is a placeholder for the processing logic.
    // In a real application, you would retrieve the file, parse it using the mapping,
    // and create the sale records.
    await Task.CompletedTask; // Example dummy async operation
    return Results.Ok(new { message = "Import processing started." });
});

importGroup.MapGet("/history", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
    var history = await db.ImportJobs
        .Where(j => j.UserId == userId)
        .OrderByDescending(j => j.StartedAt)
        .ToListAsync();
    return Results.Ok(history);
});
// fix for azure port
app.Urls.Add("http://*:8080");
app.Run();

static bool IsRunningInAzure()
{
    string? instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
    string? hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");

    return !string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(hostname);
}


public partial class Program { }
