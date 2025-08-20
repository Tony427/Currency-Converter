using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Application.Settings;
using Microsoft.Extensions.Options;

namespace CurrencyConverter.Application.Services
{
    public class CurrencyProviderFactory : ICurrencyProviderFactory
    {
        private readonly IEnumerable<ICurrencyProvider> _providers;
        private readonly CurrencySettings _settings;

        public CurrencyProviderFactory(IEnumerable<ICurrencyProvider> providers, IOptions<CurrencySettings> settings)
        {
            _providers = providers;
            _settings = settings.Value;
        }

        public ICurrencyProvider GetProvider()
        {
            var providerName = _settings.Provider?.ToLowerInvariant() ?? "frankfurter";
            
            var provider = _providers.FirstOrDefault(p => 
                p.GetType().Name.ToLowerInvariant().Contains(providerName));
            
            if (provider == null)
            {
                throw new InvalidOperationException($"Currency provider '{_settings.Provider}' not found. Available providers: {string.Join(", ", _providers.Select(p => p.GetType().Name))}");
            }
            
            return provider;
        }
    }
}
