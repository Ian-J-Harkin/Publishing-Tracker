using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Sales.Queries;
using PublishingTracker.Application.Sales.Commands;
using MediatR;

namespace PublishingTracker.Api.Features.Sales;

public static class SalesEndpoints
{
    public static void MapSalesEndpoints(this WebApplication app)
    {
        var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();

        salesGroup.MapGet("/", async (
            [FromServices] IMediator mediator,             
            [FromQuery] int? bookId,
            [FromQuery] int? platformId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate) =>
        {
            var query = new GetSalesQuery(bookId, platformId, startDate, endDate);
            var result = await mediator.Send(query);      
            
            if (!result.IsSuccess)            
                return result.ErrorMessage == "Unauthorized" ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);
            
            return Results.Ok(result.Value);
        });

        salesGroup.MapGet("/summary", async (
            [FromServices] IMediator mediator, 
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate) =>
        {
            var query = new GetSalesSummaryQuery(startDate, endDate);
            var result = await mediator.Send(query);
            
            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized" ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);
                
            return Results.Ok(result.Value);
        });

        salesGroup.MapPost("/", async ([FromServices] IMediator mediator, CreateSaleDto createSaleDto) =>
        {
            var command = new CreateSaleCommand(
                createSaleDto.BookId,
                createSaleDto.PlatformId,
                createSaleDto.SaleDate,
                createSaleDto.Quantity > 0 ? createSaleDto.Quantity : 1,
                createSaleDto.UnitPrice,
                createSaleDto.Royalty,
                createSaleDto.Currency
            );
            
            var result = await mediator.Send(command);
            
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("not found"))
                    return Results.NotFound(result.ErrorMessage);
                    
                return result.ErrorMessage == "Unauthorized" ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);
            }
                
            return Results.Created($"/api/sales/{result.Value?.Id}", result.Value);
        });
    }
}