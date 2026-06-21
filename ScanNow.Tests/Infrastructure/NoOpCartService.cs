using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Cart.DTOs;

namespace ScanNow.Tests.Infrastructure;

public sealed class NoOpCartService : ICartService
{
    public Task<CartDto> GetCartAsync(string sessionCode) => Task.FromResult(new CartDto());
    public Task UpdateCartAsync(string sessionCode, CartDto cart) => Task.CompletedTask;
    public Task ClearCartAsync(string sessionCode) => Task.CompletedTask;
}
