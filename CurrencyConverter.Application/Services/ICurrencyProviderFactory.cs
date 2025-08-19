using System.Collections.Generic;

using CurrencyConverter.Domain.Interfaces;
using System.Collections.Generic;

namespace CurrencyConverter.Application.Services
{
    public interface ICurrencyProviderFactory
    {
        ICurrencyProvider GetProvider();
    }
}