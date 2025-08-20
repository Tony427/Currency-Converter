using Asp.Versioning;
using CurrencyConverter.Application.DTOs.Currency;
using CurrencyConverter.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyConverter.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/rates")]
    [ApiVersion("1.0")]
    [Authorize]
    public class CurrencyController : ControllerBase
    {
        private readonly ICurrencyService _currencyService;

        public CurrencyController(ICurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        [HttpGet("{baseCurrency}/latest")]
        public async Task<IActionResult> GetLatestRates(string baseCurrency = "EUR")
        {
            var rates = await _currencyService.GetLatestExchangeRatesAsync(baseCurrency);
            if (rates == null || !rates.Any())
            {
                return NotFound("Could not retrieve latest rates.");
            }
            return Ok(rates);
        }

        [HttpPost("convert")]
        public async Task<IActionResult> ConvertCurrency([FromBody] ConversionRequestDto request)
        {
            var result = await _currencyService.ConvertCurrencyAsync(request);
            if (result == null)
            {
                return BadRequest("Conversion failed. Please check your input currencies and amount.");
            }
            return Ok(result);
        }

        [HttpGet("{baseCurrency}/historical")]
        public async Task<IActionResult> GetHistoricalRates(string baseCurrency, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var rates = await _currencyService.GetHistoricalExchangeRatesAsync(baseCurrency, fromDate, toDate, page, pageSize);
            if (rates == null || !rates.Any())
            {
                return NotFound("No historical rates found for the specified criteria.");
            }
            return Ok(rates);
        }
    }
}
