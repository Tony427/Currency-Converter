using System.Collections.Generic;

namespace CurrencyConverter.Application.Services
{
    public interface ICurrencyProviderFactory
    {
        ICurrencyProvider GetProvider(string providerName);
    }
}