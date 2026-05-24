using ScanNow.Application.Features.Cart.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(string sessionCode);
        Task UpdateCartAsync(string sessionCode, CartDto cart);
        Task ClearCartAsync(string sessionCode);
    }
}
