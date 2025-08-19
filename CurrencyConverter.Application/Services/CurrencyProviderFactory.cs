using CurrencyConverter.Domain.Interfaces;

namespace CurrencyConverter.Application.Services
{
    public class CurrencyProviderFactory : ICurrencyProviderFactory
    {
        private readonly ICurrencyProvider _currencyProvider;

        public CurrencyProviderFactory(ICurrencyProvider currencyProvider)
        {
            _currencyProvider = currencyProvider;
        }

        public ICurrencyProvider GetProvider()
        {
            return _currencyProvider;
        }
    }
}
