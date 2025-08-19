using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace CurrencyConverter.Infrastructure.Services
{
    public class FrankfurterApiService : ICurrencyProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FrankfurterApiService> _logger;

        public FrankfurterApiService(HttpClient httpClient, ILogger<FrankfurterApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IEnumerable<ExchangeRate>> GetLatestRatesAsync(string baseCurrency)
        {
            var requestUri = $"/latest?from={baseCurrency}";
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogInformation("[{CorrelationId}] Sending GET request to Frankfurter API: {RequestUri}", correlationId, requestUri);

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(requestUri);
                stopwatch.Stop();
                _logger.LogInformation("[{CorrelationId}] Received response from Frankfurter API: {StatusCode} in {ElapsedMilliseconds}ms", correlationId, response.StatusCode, stopwatch.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] Error sending request to Frankfurter API: {Message}", correlationId, ex.Message);
                throw;
            }

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

        public async Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate)
        {
            var requestUri = $"/{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}?from={baseCurrency}";
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogInformation("[{CorrelationId}] Sending GET request to Frankfurter API: {RequestUri}", correlationId, requestUri);

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(requestUri);
                stopwatch.Stop();
                _logger.LogInformation("[{CorrelationId}] Received response from Frankfurter API: {StatusCode} in {ElapsedMilliseconds}ms", correlationId, response.StatusCode, stopwatch.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] Error sending request to Frankfurter API: {Message}", correlationId, ex.Message);
                throw;
            }

            var content = await response.Content.ReadAsStringAsync();
            var frankfurterResponse = JsonSerializer.Deserialize<FrankfurterHistoricalResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var exchangeRates = new List<ExchangeRate>();
            foreach (var dateEntry in frankfurterResponse.Rates)
            {
                foreach (var rateEntry in dateEntry.Value)
                {
                    exchangeRates.Add(new ExchangeRate
                    {
                        BaseCurrency = frankfurterResponse.Base,
                        TargetCurrency = rateEntry.Key,
                        Rate = rateEntry.Value,
                        Date = dateEntry.Key,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            return exchangeRates;
        }

        // Helper class for deserializing Frankfurter API response
        private class FrankfurterLatestResponse
        {
            public string Base { get; set; }
            public DateTime Date { get; set; }
            public Dictionary<string, decimal> Rates { get; set; }
        }

        private class FrankfurterHistoricalResponse
        {
            public string Base { get; set; }
            public Dictionary<DateTime, Dictionary<string, decimal>> Rates { get; set; }
        }
    }
}