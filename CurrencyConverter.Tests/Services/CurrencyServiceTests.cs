using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CurrencyConverter.Application.Services;
using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Application.DTOs.Currency;
using Microsoft.Extensions.Options;
using CurrencyConverter.Application.Settings;
using System.Linq;

namespace CurrencyConverter.Tests.Services
{
    public class CurrencyServiceTests
    {
        private readonly Mock<ICurrencyProviderFactory> _mockCurrencyProviderFactory;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<ICurrencyProvider> _mockCurrencyProvider;
        private readonly CurrencyService _currencyService;
        private readonly IOptions<CurrencySettings> _currencySettings;

        public CurrencyServiceTests()
        {
            _mockCurrencyProviderFactory = new Mock<ICurrencyProviderFactory>();
            _mockCacheService = new Mock<ICacheService>();
            _mockCurrencyProvider = new Mock<ICurrencyProvider>();

            _mockCurrencyProviderFactory.Setup(f => f.GetProvider()).Returns(_mockCurrencyProvider.Object);

            _currencySettings = Options.Create(new CurrencySettings
            {
                ExcludedCurrencies = new List<string> { "TRY", "PLN", "THB", "MXN" }
            });

            _currencyService = new CurrencyService(
                _mockCurrencyProviderFactory.Object,
                _mockCacheService.Object,
                _currencySettings
            );
        }

        [Fact]
        public async Task GetLatestExchangeRatesAsync_ShouldReturnRates_WhenNotInCache()
        {
            // Arrange
            var baseCurrency = "USD";
            var expectedRates = new List<ExchangeRate>
            {
                new ExchangeRate { BaseCurrency = "USD", TargetCurrency = "EUR", Rate = 0.9m, Date = DateTime.Today },
                new ExchangeRate { BaseCurrency = "USD", TargetCurrency = "GBP", Rate = 0.8m, Date = DateTime.Today }
            };

            _mockCacheService.Setup(c => c.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<IEnumerable<ExchangeRate>>>>(), It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<IEnumerable<ExchangeRate>>> factory, TimeSpan? expiry) => factory());
            _mockCurrencyProvider.Setup(p => p.GetLatestRatesAsync(baseCurrency)).ReturnsAsync(expectedRates);

            // Act
            var result = await _currencyService.GetLatestExchangeRatesAsync(baseCurrency);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedRates.Count, result.Count());
            _mockCacheService.Verify(c => c.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<IEnumerable<ExchangeRate>>>>(), It.IsAny<TimeSpan?>()), Times.Once);
            _mockCurrencyProvider.Verify(p => p.GetLatestRatesAsync(baseCurrency), Times.Once);
        }

        [Fact]
        public async Task GetLatestExchangeRatesAsync_ShouldThrowArgumentException_WhenBaseCurrencyIsExcluded()
        {
            // Arrange
            var baseCurrency = "TRY"; // Excluded currency

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _currencyService.GetLatestExchangeRatesAsync(baseCurrency));
        }

        [Fact]
        public async Task ConvertCurrencyAsync_ShouldReturnConvertedAmount_ForDirectConversion()
        {
            // Arrange
            var request = new ConversionRequestDto { Amount = 100, FromCurrency = "USD", ToCurrency = "EUR" };
            var latestRates = new List<ExchangeRate>
            {
                new ExchangeRate { BaseCurrency = "USD", TargetCurrency = "EUR", Rate = 0.9m, Date = DateTime.Today }
            };

            _mockCacheService.Setup(c => c.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<IEnumerable<ExchangeRate>>>>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync(latestRates);

            // Act
            var result = await _currencyService.ConvertCurrencyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(90m, result.ConvertedAmount);
            Assert.Equal("USD", result.FromCurrency);
            Assert.Equal("EUR", result.ToCurrency);
            Assert.Equal(0.9m, result.Rate);
        }

        [Fact]
        public async Task ConvertCurrencyAsync_ShouldThrowArgumentException_WhenFromCurrencyIsExcluded()
        {
            // Arrange
            var request = new ConversionRequestDto { Amount = 100, FromCurrency = "PLN", ToCurrency = "EUR" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _currencyService.ConvertCurrencyAsync(request));
        }

        [Fact]
        public async Task ConvertCurrencyAsync_ShouldThrowArgumentException_WhenToCurrencyIsExcluded()
        {
            // Arrange
            var request = new ConversionRequestDto { Amount = 100, FromCurrency = "USD", ToCurrency = "THB" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _currencyService.ConvertCurrencyAsync(request));
        }

        [Fact]
        public async Task GetHistoricalExchangeRatesAsync_ShouldReturnRates_WhenNotInCache()
        {
            // Arrange
            var baseCurrency = "USD";
            var fromDate = DateTime.Today.AddDays(-7);
            var toDate = DateTime.Today;
            var page = 1;
            var pageSize = 10;
            var expectedRates = new List<ExchangeRate>
            {
                new ExchangeRate { BaseCurrency = "USD", TargetCurrency = "EUR", Rate = 0.9m, Date = DateTime.Today.AddDays(-1) },
                new ExchangeRate { BaseCurrency = "USD", TargetCurrency = "GBP", Rate = 0.8m, Date = DateTime.Today.AddDays(-2) }
            };

            _mockCacheService.Setup(c => c.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<IEnumerable<ExchangeRate>>>>(), It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<IEnumerable<ExchangeRate>>> factory, TimeSpan? expiry) => factory());
            _mockCurrencyProvider.Setup(p => p.GetHistoricalRatesAsync(baseCurrency, fromDate, toDate)).ReturnsAsync(expectedRates);

            // Act
            var result = await _currencyService.GetHistoricalExchangeRatesAsync(baseCurrency, fromDate, toDate, page, pageSize);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedRates.Count, result.Count());
            _mockCacheService.Verify(c => c.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<IEnumerable<ExchangeRate>>>>(), It.IsAny<TimeSpan?>()), Times.Once);
            _mockCurrencyProvider.Verify(p => p.GetHistoricalRatesAsync(baseCurrency, fromDate, toDate), Times.Once);
        }

        [Fact]
        public async Task GetHistoricalExchangeRatesAsync_ShouldThrowArgumentException_WhenBaseCurrencyIsExcluded()
        {
            // Arrange
            var baseCurrency = "PLN"; // Excluded currency
            var fromDate = DateTime.Today.AddDays(-7);
            var toDate = DateTime.Today;
            var page = 1;
            var pageSize = 10;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _currencyService.GetHistoricalExchangeRatesAsync(baseCurrency, fromDate, toDate, page, pageSize));
        }
    }
}
