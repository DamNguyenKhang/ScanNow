using Microsoft.AspNetCore.SignalR;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Cart.DTOs;

namespace ScanNow.Web.Hubs
{
    public class CartHub : Hub
    {
        private readonly ICartService _cartService;

        public CartHub(ICartService cartService)
        {
            _cartService = cartService;
        }

        public async Task<CartDto> JoinSession(string sessionCode)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            await Groups.AddToGroupAsync(Context.ConnectionId, normalizedCode);
            return await _cartService.GetCartAsync(normalizedCode);
        }

        public async Task UpdateCart(string sessionCode, CartDto cart)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            await _cartService.UpdateCartAsync(normalizedCode, cart);
            await Clients.OthersInGroup(normalizedCode).SendAsync("CartUpdated", cart);
        }

        public async Task ClearCart(string sessionCode)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            await _cartService.ClearCartAsync(normalizedCode);
            await Clients.OthersInGroup(normalizedCode).SendAsync("CartCleared");
        }

        public async Task LeaveSession(string sessionCode)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalizedCode);
        }
    }
}
