namespace PublishingTracker.Domain.Configuration;

/// <summary>
/// Configuration settings for currency handling in the application
/// </summary>
public class CurrencySettings
{
    public const string SectionName = "Currency";

    /// <summary>
    /// The default currency code to use when no currency is specified (e.g., "USD", "EUR", "GBP")
    /// </summary>
    public string DefaultCurrency { get; set; } = "USD";

    /// <summary>
    /// The base currency for all financial calculations and reporting
    /// </summary>
    public string BaseCurrency { get; set; } = "USD";

    /// <summary>
    /// List of supported currency codes in the application
    /// </summary>
    public List<string> SupportedCurrencies { get; set; } = new() 
    { 
        "USD", "EUR", "GBP", "CAD", "AUD", "JPY", "CNY", "INR" 
    };
}