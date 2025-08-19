using CurrencyConverter.Application.DTOs.Currency;
using CurrencyConverter.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.Extensions.Options;
using CurrencyConverter.Application.Settings;

namespace CurrencyConverter.Application.Services
{
    public class CurrencyService : ICurrencyService
    {
        private readonly ICurrencyProviderFactory _currencyProviderFactory;
        private readonly ICacheService _cacheService;
        private readonly HashSet<string> _excludedCurrencies;

        public CurrencyService(ICurrencyProviderFactory currencyProviderFactory, ICacheService cacheService, IOptions<CurrencySettings> currencySettings)
        {
            _currencyProviderFactory = currencyProviderFactory;
            _cacheService = cacheService;
            _excludedCurrencies = new HashSet<string>(currencySettings.Value.ExcludedCurrencies, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<IEnumerable<ExchangeRate>> GetLatestExchangeRatesAsync(string baseCurrency)
        {
            if (_excludedCurrencies.Contains(baseCurrency.ToUpper()))
            {
                throw new ArgumentException($"Currency {baseCurrency} is excluded.");
            }

            var cacheKey = $"latest_rates_{baseCurrency.ToUpper()}";
            var rates = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var provider = _currencyProviderFactory.GetProvider();
                var fetchedRates = await provider.GetLatestRatesAsync(baseCurrency);
                if (fetchedRates != null)
                {
                    // Filter out excluded target currencies before caching and returning
                    return fetchedRates.Where(r => !_excludedCurrencies.Contains(r.TargetCurrency.ToUpper())).ToList();
                }
                return null;
            }, TimeSpan.FromMinutes(15));

            return rates;
        }

        public async Task<ConversionResponseDto> ConvertCurrencyAsync(ConversionRequestDto request)
        {
            if (_excludedCurrencies.Contains(request.FromCurrency.ToUpper()) || _excludedCurrencies.Contains(request.ToCurrency.ToUpper()))
            {
                throw new ArgumentException("One of the currencies is excluded.");
            }

            // For simplicity, we'll fetch latest rates and calculate. In a real scenario, 
            // you might fetch specific rates or use a more complex conversion logic.
            var latestRates = await GetLatestExchangeRatesAsync(request.FromCurrency);
            if (latestRates == null)
            {
                return null; // Or throw a specific exception
            }

            var targetRate = latestRates.FirstOrDefault(r => r.TargetCurrency.Equals(request.ToCurrency, StringComparison.OrdinalIgnoreCase));

            if (targetRate == null)
            {
                // If direct rate not found, try inverse or via EUR/USD as common base
                // This is a simplified approach. A robust solution would involve a graph traversal.
                var inverseRate = latestRates.FirstOrDefault(r => r.TargetCurrency.Equals(request.FromCurrency, StringComparison.OrdinalIgnoreCase));
                if (inverseRate != null && request.FromCurrency.Equals(request.ToCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    // Converting to itself, return original amount
                    return new ConversionResponseDto
                    {
                        ConvertedAmount = request.Amount,
                        FromCurrency = request.FromCurrency,
                        ToCurrency = request.ToCurrency,
                        Rate = 1m,
                        Date = DateTime.UtcNow
                    };
                }
                else if (request.FromCurrency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
                {
                    // If from EUR, and target not found, something is wrong with data or target is excluded
                    return null;
                }
                else
                {
                    // Try converting from request.FromCurrency to EUR, then EUR to request.ToCurrency
                    var fromEurRate = latestRates.FirstOrDefault(r => r.TargetCurrency.Equals("EUR", StringComparison.OrdinalIgnoreCase));
                    if (fromEurRate != null)
                    {
                        var toEurAmount = request.Amount / fromEurRate.Rate;
                        var eurToTargetRates = await GetLatestExchangeRatesAsync("EUR");
                        var eurToTargetRate = eurToTargetRates?.FirstOrDefault(r => r.TargetCurrency.Equals(request.ToCurrency, StringComparison.OrdinalIgnoreCase));
                        if (eurToTargetRate != null)
                        {
                            var convertedAmount = toEurAmount * eurToTargetRate.Rate;
                            return new ConversionResponseDto
                            {
                                FromCurrency = request.FromCurrency,
                            ToCurrency = request.ToCurrency,
                            ConvertedAmount = convertedAmount,
                            Rate = (eurToTargetRate.Rate / fromEurRate.Rate),
                            Date = DateTime.UtcNow
                            };
                        }
                    }
                }
                return null; // Could not find a conversion path
            }

            var converted = request.Amount * targetRate.Rate;
            return new ConversionResponseDto
            {
                ConvertedAmount = converted,
                FromCurrency = request.FromCurrency,
                ToCurrency = request.ToCurrency,
                Rate = targetRate.Rate,
                Date = DateTime.UtcNow
            };
        }

        public async Task<IEnumerable<ExchangeRate>> GetHistoricalExchangeRatesAsync(string baseCurrency, DateTime fromDate, DateTime toDate, int page, int pageSize)
        {
            if (_excludedCurrencies.Contains(baseCurrency.ToUpper()))
            {
                throw new ArgumentException($"Currency {baseCurrency} is excluded.");
            }

            

            var cacheKey = $"historical_rates_{baseCurrency.ToUpper()}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}_{page}_{pageSize}";
            var rates = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var provider = _currencyProviderFactory.GetProvider();
                var fetchedRates = await provider.GetHistoricalRatesAsync(baseCurrency, fromDate, toDate);

                if (fetchedRates != null)
                {
                    // Apply pagination and filter excluded target currencies
                    return fetchedRates
                        .Where(r => !_excludedCurrencies.Contains(r.TargetCurrency.ToUpper()))
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                }
                return null;
            }, TimeSpan.FromHours(24));

            return rates;
        }
    }
}
