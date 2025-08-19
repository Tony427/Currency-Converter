using System;
using System.Threading.Tasks;

namespace CurrencyConverter.Application.Services
{
    public interface ICacheService
    {
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpirationRelativeToNow = null);
        void Remove(string key);
    }
}