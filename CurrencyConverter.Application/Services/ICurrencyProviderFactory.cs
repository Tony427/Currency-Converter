using CurrencyConverter.Domain.Interfaces;

namespace CurrencyConverter.Application.Services
{
    public interface ICurrencyProviderFactory
    {
        ICurrencyProvider GetProvider();
    }
}