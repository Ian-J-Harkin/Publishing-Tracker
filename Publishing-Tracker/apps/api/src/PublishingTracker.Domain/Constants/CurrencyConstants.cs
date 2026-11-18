namespace PublishingTracker.Domain.Constants;

/// <summary>
/// Currency-related constants for the application
/// Fallback values when configuration is not available
/// </summary>
public static class CurrencyConstants
{
    /// <summary>
    /// Fallback default currency if not specified in configuration
    /// </summary>
    public const string DefaultCurrency = "USD";

    /// <summary>
    /// Fallback base currency if not specified in configuration
    /// </summary>
    public const string BaseCurrency = "USD";

    /// <summary>
    /// Standard exchange rate when no conversion is needed (same currency)
    /// </summary>
    public const decimal NoConversionRate = 1.0m;
}