using ScanNow.Application.Features.Checkout.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface ICheckoutService
    {
        Task<CheckoutResponse> CreateCheckoutAsync(string sessionCode, CreateCheckoutRequest request);
        Task<PaymentStatusResponse> GetPaymentStatusAsync(string sessionCode);
        Task<PaymentStatusResponse> CancelPendingPaymentAsync(string sessionCode);
    }
}
