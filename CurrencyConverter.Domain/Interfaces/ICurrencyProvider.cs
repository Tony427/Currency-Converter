using CurrencyConverter.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace CurrencyConverter.Domain.Interfaces
{
    public interface ICurrencyProvider
    {
        Task<IEnumerable<ExchangeRate>> GetLatestRatesAsync(string baseCurrency);
        Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate);
    }
}
