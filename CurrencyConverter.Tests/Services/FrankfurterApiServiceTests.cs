using CurrencyConverter.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text.Json;
using Polly.CircuitBreaker;

namespace CurrencyConverter.Tests.Services
{
    public class FrankfurterApiServiceTests : IDisposable
    {
        private readonly MockHttpMessageHandler _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<FrankfurterApiService>> _mockLogger;
        private readonly FrankfurterApiService _service;

        public FrankfurterApiServiceTests()
        {
            _mockHttpMessageHandler = new MockHttpMessageHandler();
            _httpClient = new HttpClient(_mockHttpMessageHandler)
            {
                BaseAddress = new Uri("https://api.frankfurter.app/")
            };
            _mockLogger = new Mock<ILogger<FrankfurterApiService>>();
            _service = new FrankfurterApiService(_httpClient, _mockLogger.Object);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _mockHttpMessageHandler.Dispose();
        }

        [Fact]
        public async Task GetLatestRatesAsync_ValidRequest_ReturnsExchangeRates()
        {
            // Arrange
            var baseCurrency = "EUR";
            var responseContent = JsonSerializer.Serialize(new
            {
                Base = "EUR",
                Date = DateTime.UtcNow.Date,
                Rates = new Dictionary<string, decimal>
                {
                    { "USD", 1.0954m },
                    { "GBP", 0.8654m },
                    { "JPY", 149.32m }
                }
            });

            _mockHttpMessageHandler
                .When($"/latest?from={baseCurrency}")
                .Respond("application/json", responseContent);

            // Act
            var result = await _service.GetLatestRatesAsync(baseCurrency);

            // Assert
            Assert.NotNull(result);
            var exchangeRates = result.ToList();
            Assert.Equal(3, exchangeRates.Count);
            
            var usdRate = exchangeRates.First(r => r.TargetCurrency == "USD");
            Assert.Equal("EUR", usdRate.BaseCurrency);
            Assert.Equal(1.0954m, usdRate.Rate);
            Assert.Equal(DateTime.UtcNow.Date, usdRate.Date.Date);

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending GET request to Frankfurter API")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetLatestRatesAsync_HttpRequestException_RetriesAndThrows()
        {
            // Arrange
            var baseCurrency = "EUR";
            
            _mockHttpMessageHandler
                .When($"/latest?from={baseCurrency}")
                .Throw(new HttpRequestException("Network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetLatestRatesAsync(baseCurrency));

            // Verify retry logging (should be called multiple times due to retries)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(1));
        }

        [Fact]
        public async Task GetLatestRatesAsync_ServerError_RetriesAndThrows()
        {
            // Arrange
            var baseCurrency = "EUR";
            
            _mockHttpMessageHandler
                .When($"/latest?from={baseCurrency}")
                .Respond(HttpStatusCode.InternalServerError);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetLatestRatesAsync(baseCurrency));

            // Verify retry logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(1));
        }

        [Fact]
        public async Task GetLatestRatesAsync_SuccessAfterRetry_ReturnsData()
        {
            // Arrange
            var baseCurrency = "EUR";
            var responseContent = JsonSerializer.Serialize(new
            {
                Base = "EUR",
                Date = DateTime.UtcNow.Date,
                Rates = new Dictionary<string, decimal> { { "USD", 1.0954m } }
            });

            var mockRequest = _mockHttpMessageHandler.When($"/latest?from={baseCurrency}");
            mockRequest.Respond(HttpStatusCode.InternalServerError); // First call fails
            mockRequest.Respond("application/json", responseContent); // Second call succeeds

            // Act
            var result = await _service.GetLatestRatesAsync(baseCurrency);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);

            // Verify retry logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(1));
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ValidRequest_ReturnsExchangeRates()
        {
            // Arrange
            var baseCurrency = "EUR";
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            
            var responseContent = JsonSerializer.Serialize(new
            {
                Base = "EUR",
                Rates = new Dictionary<DateTime, Dictionary<string, decimal>>
                {
                    { fromDate, new Dictionary<string, decimal> { { "USD", 1.0954m }, { "GBP", 0.8654m } } },
                    { toDate, new Dictionary<string, decimal> { { "USD", 1.0960m }, { "GBP", 0.8660m } } }
                }
            });

            _mockHttpMessageHandler
                .When($"/{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}?from={baseCurrency}")
                .Respond("application/json", responseContent);

            // Act
            var result = await _service.GetHistoricalRatesAsync(baseCurrency, fromDate, toDate);

            // Assert
            Assert.NotNull(result);
            var exchangeRates = result.ToList();
            Assert.Equal(4, exchangeRates.Count); // 2 dates × 2 currencies

            var firstDayUsd = exchangeRates.First(r => r.Date.Date == fromDate.Date && r.TargetCurrency == "USD");
            Assert.Equal("EUR", firstDayUsd.BaseCurrency);
            Assert.Equal(1.0954m, firstDayUsd.Rate);

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending GET request to Frankfurter API")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_HttpRequestException_RetriesAndThrows()
        {
            // Arrange
            var baseCurrency = "EUR";
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            
            _mockHttpMessageHandler
                .When($"/{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}?from={baseCurrency}")
                .Throw(new HttpRequestException("Network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetHistoricalRatesAsync(baseCurrency, fromDate, toDate));

            // Verify retry logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(1));
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_EmptyRatesResponse_ReturnsEmptyList()
        {
            // Arrange
            var baseCurrency = "EUR";
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            
            var responseContent = JsonSerializer.Serialize(new
            {
                Base = "EUR",
                Rates = new Dictionary<DateTime, Dictionary<string, decimal>>()
            });

            _mockHttpMessageHandler
                .When($"/{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}?from={baseCurrency}")
                .Respond("application/json", responseContent);

            // Act
            var result = await _service.GetHistoricalRatesAsync(baseCurrency, fromDate, toDate);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLatestRatesAsync_InvalidJsonResponse_ThrowsJsonException()
        {
            // Arrange
            var baseCurrency = "EUR";
            var invalidJson = "invalid json content";

            _mockHttpMessageHandler
                .When($"/latest?from={baseCurrency}")
                .Respond("application/json", invalidJson);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(() => _service.GetLatestRatesAsync(baseCurrency));
        }

        [Fact]
        public async Task GetLatestRatesAsync_LogsCorrelationId_ForEachRequest()
        {
            // Arrange
            var baseCurrency = "EUR";
            var responseContent = JsonSerializer.Serialize(new
            {
                Base = "EUR",
                Date = DateTime.UtcNow.Date,
                Rates = new Dictionary<string, decimal> { { "USD", 1.0954m } }
            });

            _mockHttpMessageHandler
                .When($"/latest?from={baseCurrency}")
                .Respond("application/json", responseContent);

            // Act
            await _service.GetLatestRatesAsync(baseCurrency);

            // Assert - Verify correlation ID is used in logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("[") && v.ToString().Contains("]")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(2)); // Request start and response logs
        }

        [Fact]
        public async Task GetLatestRatesAsync_LogsExecutionTime_InResponse()
        {
            // Arrange
            var baseCurrency = "EUR";
            var responseContent = JsonSerializer.Serialize(new
            {
                Base = "EUR",
                Date = DateTime.UtcNow.Date,
                Rates = new Dictionary<string, decimal> { { "USD", 1.0954m } }
            });

            _mockHttpMessageHandler
                .When($"/latest?from={baseCurrency}")
                .Respond("application/json", responseContent);

            // Act
            await _service.GetLatestRatesAsync(baseCurrency);

            // Assert - Verify response time is logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("ms")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(1));
        }
    }
}