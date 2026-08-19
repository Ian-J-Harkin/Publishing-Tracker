namespace PublishingTracker.Application.Common.Interfaces;

public interface ICurrencyConverterService
{
    decimal GetExchangeRate(string fromCurrency, string toCurrency = "USD");
}