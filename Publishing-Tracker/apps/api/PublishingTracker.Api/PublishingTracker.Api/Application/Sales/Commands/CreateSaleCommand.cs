using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Api.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Api.Extensions;

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
    private readonly PublishingTrackerDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateSaleCommandHandler(PublishingTrackerDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        var sale = Sale.Create(
            request.BookId,
            request.PlatformId,
            request.SaleDate,
            request.Quantity > 0 ? request.Quantity : 1,
            request.UnitPrice,
            request.Royalty,
            request.Currency
        );

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SaleDto>.Success(sale.ToDto(book.Title, platform.Name));
    }
}
