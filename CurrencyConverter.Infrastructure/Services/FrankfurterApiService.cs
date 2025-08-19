using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Domain.Entities;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Polly;
using Polly.Extensions.Http;

namespace CurrencyConverter.Infrastructure.Services
{
    public class FrankfurterApiService : ICurrencyProvider
    {
        private readonly HttpClient _httpClient;

        public FrankfurterApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<ExchangeRate>> GetLatestRatesAsync(string baseCurrency)
        {
            var response = await _httpClient.GetAsync($"/latest?from={baseCurrency}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var frankfurterResponse = JsonSerializer.Deserialize<FrankfurterLatestResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var exchangeRates = new List<ExchangeRate>();
            foreach (var rate in frankfurterResponse.Rates)
            {
                exchangeRates.Add(new ExchangeRate
                {
                    BaseCurrency = frankfurterResponse.Base,
                    TargetCurrency = rate.Key,
                    Rate = rate.Value,
                    Date = frankfurterResponse.Date,
                    CreatedAt = DateTime.UtcNow
                });
            }
            return exchangeRates;
        }

        public Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate)
        {
            throw new NotImplementedException();
        }

        // Helper class for deserializing Frankfurter API response
        private class FrankfurterLatestResponse
        {
            public string Base { get; set; }
            public DateTime Date { get; set; }
            public Dictionary<string, decimal> Rates { get; set; }
        }
    }
}