using Microsoft.Extensions.Caching.Memory;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Cart.DTOs;

namespace ScanNow.Application.Features.Cart
{
    public class CartService : ICartService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(6);

        public CartService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<CartDto> GetCartAsync(string sessionCode)
        {
            var key = GetCacheKey(sessionCode);
            if (!_cache.TryGetValue(key, out CartDto? cart) || cart == null)
            {
                cart = new CartDto();
            }

            return Task.FromResult(cart);
        }

        public Task UpdateCartAsync(string sessionCode, CartDto cart)
        {
            var key = GetCacheKey(sessionCode);
            _cache.Set(key, cart, DefaultCacheDuration);
            return Task.CompletedTask;
        }

        public Task ClearCartAsync(string sessionCode)
        {
            var key = GetCacheKey(sessionCode);
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        private static string GetCacheKey(string sessionCode) => $"Cart_{sessionCode.ToUpperInvariant()}";
    }
}
