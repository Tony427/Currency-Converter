using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Domain.Interfaces;

namespace CurrencyConverter.Tests.Integration
{
    public class MockCurrencyProvider : ICurrencyProvider
    {
        public async Task<IEnumerable<ExchangeRate>> GetLatestRatesAsync(string baseCurrency)
        {
            await Task.Delay(10); // Simulate async call
            
            return new List<ExchangeRate>
            {
                new ExchangeRate { BaseCurrency = baseCurrency, TargetCurrency = "EUR", Rate = 0.85m, Date = DateTime.Today },
                new ExchangeRate { BaseCurrency = baseCurrency, TargetCurrency = "GBP", Rate = 0.73m, Date = DateTime.Today },
                new ExchangeRate { BaseCurrency = baseCurrency, TargetCurrency = "JPY", Rate = 110.5m, Date = DateTime.Today }
            };
        }

        public async Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate)
        {
            await Task.Delay(10); // Simulate async call
            
            var rates = new List<ExchangeRate>();
            var currentDate = fromDate;
            
            while (currentDate <= toDate)
            {
                rates.AddRange(new[]
                {
                    new ExchangeRate { BaseCurrency = baseCurrency, TargetCurrency = "EUR", Rate = 0.85m + (decimal)(currentDate.Day * 0.001), Date = currentDate },
                    new ExchangeRate { BaseCurrency = baseCurrency, TargetCurrency = "GBP", Rate = 0.73m + (decimal)(currentDate.Day * 0.001), Date = currentDate }
                });
                currentDate = currentDate.AddDays(1);
            }
            
            return rates;
        }
    }
}