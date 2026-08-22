using MediatR;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;

using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;


namespace PublishingTracker.Application.Sales.Commands;

public record CreateSaleCommand(
    int BookId, 
    int PlatformId, 
    DateTime SaleDate, 
    int Quantity, 
    decimal UnitPrice, 
    decimal Royalty, 
    string Currency) : IRequest<Result<SaleDto>>;

public class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Result<SaleDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyConverterService _currencyConverterService;

    public CreateSaleCommandHandler(IApplicationDbContext context, IQueryExecutor queries, ICurrentUserService currentUserService, ICurrencyConverterService currencyConverterService)
    {
        _context = context;
        _queries = queries;
        _currentUserService = currentUserService;
        _currencyConverterService = currencyConverterService;
    }

    public async Task<Result<SaleDto>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<SaleDto>.Failure("Unauthorized");

        var book = await _queries.FirstOrDefaultAsync(_context.Books.Where(b => b.Id == request.BookId && b.UserId == userId), cancellationToken);
        if (book == null)
            return Result<SaleDto>.Failure("Book not found or does not belong to the user.");

        var platform = await _queries.FirstOrDefaultAsync(_context.Platforms.Where(p => p.Id == request.PlatformId), cancellationToken);
        if (platform == null)
            return Result<SaleDto>.Failure("Platform not found.");

        var exchangeRate = _currencyConverterService.GetExchangeRate(request.Currency, "USD");

        var sale = Sale.Create(
            request.BookId,
            request.PlatformId,
            request.SaleDate,
            request.Quantity > 0 ? request.Quantity : 1,
            request.UnitPrice,
            request.Royalty,
            platform.CommissionRate,
            exchangeRate,
            request.Currency
        );

        _context.Add(sale);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SaleDto>.Success(sale.ToDto(book.Title, platform.Name));
    }
}
