using CurrencyConverter.Application.DTOs.Currency;
using CurrencyConverter.Domain.Entities;

namespace CurrencyConverter.Application.Services
{
    public interface ICurrencyService
    {
        Task<IEnumerable<ExchangeRate>> GetLatestExchangeRatesAsync(string baseCurrency);
        Task<ConversionResponseDto> ConvertCurrencyAsync(ConversionRequestDto request);
        Task<IEnumerable<ExchangeRate>> GetHistoricalExchangeRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate, int page, int pageSize);
    }
}
