using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace CurrencyConverter.Infrastructure.Services
{
    public class FrankfurterApiService : ICurrencyProvider
    {
        private static readonly ActivitySource ActivitySource = new("CurrencyConverter.Infrastructure.FrankfurterApi");
        
        private readonly HttpClient _httpClient;
        private readonly ILogger<FrankfurterApiService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _resilientPolicy;

        public FrankfurterApiService(HttpClient httpClient, ILogger<FrankfurterApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Configure retry policy with exponential backoff
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry attempt {RetryCount} for Frankfurter API request. Waiting {Delay}ms before next attempt. Reason: {Reason}",
                            retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                    });

            // Configure circuit breaker policy
            _circuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError("Circuit breaker opened for Frankfurter API. Duration: {Duration}s. Reason: {Reason}",
                            duration.TotalSeconds, exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset for Frankfurter API");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open for Frankfurter API");
                    });

            // Combine retry and circuit breaker policies
            _resilientPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);
        }

        public async Task<IEnumerable<ExchangeRate>> GetLatestRatesAsync(string baseCurrency)
        {
            using var activity = ActivitySource.StartActivity("GetLatestRates");
            activity?.SetTag("currency.base", baseCurrency);
            activity?.SetTag("frankfurter.operation", "latest");

            var requestUri = $"/latest?from={baseCurrency}";
            var correlationId = Guid.NewGuid().ToString();
            activity?.SetTag("correlation.id", correlationId);
            
            _logger.LogInformation("[{CorrelationId}] Sending GET request to Frankfurter API: {RequestUri}", correlationId, requestUri);

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await _resilientPolicy.ExecuteAsync(async () =>
                {
                    var httpResponse = await _httpClient.GetAsync(requestUri);
                    httpResponse.EnsureSuccessStatusCode();
                    return httpResponse;
                });
                
                stopwatch.Stop();
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
                
                _logger.LogInformation("[{CorrelationId}] Received response from Frankfurter API: {StatusCode} in {ElapsedMilliseconds}ms", 
                    correlationId, response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("CircuitBreaker"))
            {
                stopwatch.Stop();
                activity?.SetTag("error.type", "circuit_breaker_open");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, "Circuit breaker is open");
                
                _logger.LogError(ex, "[{CorrelationId}] Circuit breaker is open for Frankfurter API: {Message}", correlationId, ex.Message);
                throw new HttpRequestException("Currency service is temporarily unavailable due to external API issues", ex);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                activity?.SetTag("error.type", "http_request_exception");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, "HTTP request failed after retries");
                
                _logger.LogError(ex, "[{CorrelationId}] Error sending request to Frankfurter API after retries: {Message}", correlationId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetTag("error.type", "unexpected_exception");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, "Unexpected error");
                
                _logger.LogError(ex, "[{CorrelationId}] Unexpected error calling Frankfurter API: {Message}", correlationId, ex.Message);
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
            
            activity?.SetTag("rates.count", exchangeRates.Count);
            activity?.SetTag("response.date", frankfurterResponse.Date.ToString("yyyy-MM-dd"));
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return exchangeRates;
        }

        public async Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate)
        {
            using var activity = ActivitySource.StartActivity("GetHistoricalRates");
            activity?.SetTag("currency.base", baseCurrency);
            activity?.SetTag("frankfurter.operation", "historical");
            activity?.SetTag("date.from", fromDate.ToString("yyyy-MM-dd"));
            activity?.SetTag("date.to", toDate.ToString("yyyy-MM-dd"));

            var requestUri = $"/{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}?from={baseCurrency}";
            var correlationId = Guid.NewGuid().ToString();
            activity?.SetTag("correlation.id", correlationId);
            
            _logger.LogInformation("[{CorrelationId}] Sending GET request to Frankfurter API: {RequestUri}", correlationId, requestUri);

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await _resilientPolicy.ExecuteAsync(async () =>
                {
                    var httpResponse = await _httpClient.GetAsync(requestUri);
                    httpResponse.EnsureSuccessStatusCode();
                    return httpResponse;
                });
                
                stopwatch.Stop();
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
                
                _logger.LogInformation("[{CorrelationId}] Received response from Frankfurter API: {StatusCode} in {ElapsedMilliseconds}ms", 
                    correlationId, response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("CircuitBreaker"))
            {
                stopwatch.Stop();
                activity?.SetTag("error.type", "circuit_breaker_open");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, "Circuit breaker is open");
                
                _logger.LogError(ex, "[{CorrelationId}] Circuit breaker is open for Frankfurter API: {Message}", correlationId, ex.Message);
                throw new HttpRequestException("Currency service is temporarily unavailable due to external API issues", ex);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                activity?.SetTag("error.type", "http_request_exception");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, "HTTP request failed after retries");
                
                _logger.LogError(ex, "[{CorrelationId}] Error sending request to Frankfurter API after retries: {Message}", correlationId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetTag("error.type", "unexpected_exception");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, "Unexpected error");
                
                _logger.LogError(ex, "[{CorrelationId}] Unexpected error calling Frankfurter API: {Message}", correlationId, ex.Message);
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
            
            activity?.SetTag("rates.count", exchangeRates.Count);
            activity?.SetTag("date.range.days", (toDate - fromDate).Days);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return exchangeRates;
        }

        // Helper class for deserializing Frankfurter API response
        private class FrankfurterLatestResponse
        {
            public string Base { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public Dictionary<string, decimal> Rates { get; set; } = new();
        }

        private class FrankfurterHistoricalResponse
        {
            public string Base { get; set; } = string.Empty;
            public Dictionary<DateTime, Dictionary<string, decimal>> Rates { get; set; } = new();
        }
    }
}