using Microsoft.Extensions.Caching.Memory;

namespace CurrencyConverter.Application.Services
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;

        public MemoryCacheService(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpirationRelativeToNow = null)
        {
            if (_memoryCache.TryGetValue(key, out T value))
            {
                return value;
            }

            value = await factory();

            if (value != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions();
                if (absoluteExpirationRelativeToNow.HasValue)
                {
                    cacheEntryOptions.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow.Value;
                }
                else
                {
                    // Default expiration if not specified
                    cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                }
                _memoryCache.Set(key, value, cacheEntryOptions);
            }

            return value;
        }

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
        }
    }
}
