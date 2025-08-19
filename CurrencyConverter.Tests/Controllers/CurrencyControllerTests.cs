using CurrencyConverter.API.Controllers;
using CurrencyConverter.Application.DTOs.Currency;
using CurrencyConverter.Application.Services;
using CurrencyConverter.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CurrencyConverter.Tests.Controllers
{
    public class CurrencyControllerTests
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly CurrencyController _controller;

        public CurrencyControllerTests()
        {
            _mockCurrencyService = new Mock<ICurrencyService>();
            _controller = new CurrencyController(_mockCurrencyService.Object);
        }

        [Fact]
        public async Task GetLatestRates_ReturnsOkResult_WithRates()
        {
            // Arrange
            var expectedRates = new List<ExchangeRate>
            {
                new ExchangeRate { BaseCurrency = "EUR", TargetCurrency = "USD", Rate = 1.1m }
            };
            _mockCurrencyService.Setup(s => s.GetLatestExchangeRatesAsync(It.IsAny<string>()))
                .ReturnsAsync(expectedRates);

            // Act
            var result = await _controller.GetLatestRates("EUR");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var rates = Assert.IsAssignableFrom<IEnumerable<ExchangeRate>>(okResult.Value);
            Assert.Equal(expectedRates.Count, rates.Count());
        }

        [Fact]
        public async Task GetLatestRates_ReturnsNotFound_WhenNoRatesFound()
        {
            // Arrange
            _mockCurrencyService.Setup(s => s.GetLatestExchangeRatesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ExchangeRate>());

            // Act
            var result = await _controller.GetLatestRates("XYZ");

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task ConvertCurrency_ReturnsOkResult_WithConvertedAmount()
        {
            // Arrange
            var request = new ConversionRequestDto { Amount = 100, FromCurrency = "USD", ToCurrency = "EUR" };
            var expectedResponse = new ConversionResponseDto { ConvertedAmount = 90, FromCurrency = "USD", ToCurrency = "EUR" };
            _mockCurrencyService.Setup(s => s.ConvertCurrencyAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ConvertCurrency(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ConversionResponseDto>(okResult.Value);
            Assert.Equal(expectedResponse.ConvertedAmount, response.ConvertedAmount);
        }

        [Fact]
        public async Task ConvertCurrency_ReturnsBadRequest_WhenConversionFails()
        {
            // Arrange
            var request = new ConversionRequestDto { Amount = 100, FromCurrency = "USD", ToCurrency = "XYZ" };
            _mockCurrencyService.Setup(s => s.ConvertCurrencyAsync(request))
                .ReturnsAsync((ConversionResponseDto?)null);

            // Act
            var result = await _controller.ConvertCurrency(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetHistoricalRates_ReturnsOkResult_WithRates()
        {
            // Arrange
            var request = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-7), ToDate = DateTime.Today, Page = 1, PageSize = 10 };
            var expectedRates = new List<ExchangeRate>
            {
                new ExchangeRate { BaseCurrency = "USD", TargetCurrency = "EUR", Rate = 0.9m, Date = DateTime.Today.AddDays(-1) }
            };
            _mockCurrencyService.Setup(s => s.GetHistoricalExchangeRatesAsync(request.BaseCurrency, request.FromDate, request.ToDate, request.Page, request.PageSize))
                .ReturnsAsync(expectedRates);

            // Act
            var result = await _controller.GetHistoricalRates(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var rates = Assert.IsAssignableFrom<IEnumerable<ExchangeRate>>(okResult.Value);
            Assert.Equal(expectedRates.Count, rates.Count());
        }

        [Fact]
        public async Task GetHistoricalRates_ReturnsNotFound_WhenNoRatesFound()
        {
            // Arrange
            var request = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-7), ToDate = DateTime.Today, Page = 1, PageSize = 10 };
            _mockCurrencyService.Setup(s => s.GetHistoricalExchangeRatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ExchangeRate>());

            // Act
            var result = await _controller.GetHistoricalRates(request);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
