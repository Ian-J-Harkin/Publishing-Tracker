using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;

using PublishingTracker.Domain.Dtos;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyConverterService _currencyConverterService;

    public CreateSaleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, ICurrencyConverterService currencyConverterService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _currencyConverterService = currencyConverterService;
    }

    public async Task<Result<SaleDto>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<SaleDto>.Failure("Unauthorized");

        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == request.BookId && b.UserId == userId, cancellationToken);
        if (book == null)
            return Result<SaleDto>.Failure("Book not found or does not belong to the user.");

        var platform = await _context.Platforms.FirstOrDefaultAsync(p => p.Id == request.PlatformId, cancellationToken);
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

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SaleDto>.Success(sale.ToDto(book.Title, platform.Name));
    }
}
