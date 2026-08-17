using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Infrastructure.Services;

public class StaticCurrencyConverterService : ICurrencyConverterService
{
    public decimal GetExchangeRate(string fromCurrency, string toCurrency = "USD")
    {
        fromCurrency = fromCurrency.ToUpperInvariant();
        toCurrency = toCurrency.ToUpperInvariant();

        if (fromCurrency == toCurrency) return 1.0m;

        // Base currency is USD. Hardcoded exchange rates for MVP.
        if (toCurrency == "USD")
        {
            return fromCurrency switch
            {
                "EUR" => 1.10m,
                "GBP" => 1.30m,
                "CAD" => 0.75m,
                "AUD" => 0.65m,
                _ => 1.0m // Fallback if unknown
            };
        }

        // If converting TO something else from USD (not currently needed, but for completeness)
        if (fromCurrency == "USD")
        {
            return toCurrency switch
            {
                "EUR" => 1m / 1.10m,
                "GBP" => 1m / 1.30m,
                "CAD" => 1m / 0.75m,
                "AUD" => 1m / 0.65m,
                _ => 1.0m
            };
        }

        // Cross rate: from -> USD -> to
        var toUsdRate = GetExchangeRate(fromCurrency, "USD");
        var fromUsdToTargetRate = GetExchangeRate("USD", toCurrency);
        return toUsdRate * fromUsdToTargetRate;
    }
}
