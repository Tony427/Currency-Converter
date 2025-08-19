using CurrencyConverter.Application.DTOs.Currency;
using CurrencyConverter.Domain.Entities;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace CurrencyConverter.Tests.Integration
{
    public class CurrencyIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;

        public CurrencyIntegrationTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GetLatestRates_ReturnsOkResult()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rates/latest?base=USD");
            // Add a valid JWT token here for authorization
            // request.Headers.Add("Authorization", "Bearer YOUR_VALID_JWT_TOKEN");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var rates = JsonConvert.DeserializeObject<IEnumerable<ExchangeRate>>(content);
            Assert.NotNull(rates);
            Assert.True(rates.Any());
        }

        [Fact]
        public async Task ConvertCurrency_ReturnsOkResult()
        {
            // Arrange
            var conversionRequest = new ConversionRequestDto
            {
                Amount = 100,
                FromCurrency = "USD",
                ToCurrency = "EUR"
            };
            var requestContent = new StringContent(JsonConvert.SerializeObject(conversionRequest), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rates/convert")
            {
                Content = requestContent
            };
            // Add a valid JWT token here for authorization
            // request.Headers.Add("Authorization", "Bearer YOUR_VALID_JWT_TOKEN");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ConversionResponseDto>(content);
            Assert.NotNull(result);
            Assert.True(result.ConvertedAmount > 0);
        }

        [Fact]
        public async Task GetHistoricalRates_ReturnsOkResult()
        {
            // Arrange
            var fromDate = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
            var toDate = DateTime.Today.ToString("yyyy-MM-dd");
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/rates/historical?base=USD&fromDate={fromDate}&toDate={toDate}&page=1&pageSize=10");
            // Add a valid JWT token here for authorization
            // request.Headers.Add("Authorization", "Bearer YOUR_VALID_JWT_TOKEN");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var rates = JsonConvert.DeserializeObject<IEnumerable<ExchangeRate>>(content);
            Assert.NotNull(rates);
            Assert.True(rates.Any());
        }
    }
}
