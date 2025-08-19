using CurrencyConverter.Domain.Entities;

namespace CurrencyConverter.Domain.Interfaces
{
    public interface ICurrencyProvider
    {
        Task<IEnumerable<ExchangeRate>> GetLatestRatesAsync(string baseCurrency);
        Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate);
    }
}
