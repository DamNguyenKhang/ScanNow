using FluentValidation;
using ScanNow.Application.Features.Checkout.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.Checkout.Validators
{
    public class CreateCheckoutRequestValidator : AbstractValidator<CreateCheckoutRequest>
    {
        private static readonly PaymentMethod[] SupportedMethods = [PaymentMethod.PAYOS, PaymentMethod.CASH];

        public CreateCheckoutRequestValidator()
        {
            RuleFor(x => x.PaymentMethod)
                .IsInEnum().WithMessage("Invalid payment method.")
                .Must(m => SupportedMethods.Contains(m))
                .WithMessage($"Supported payment methods: {string.Join(", ", SupportedMethods)}");
        }
    }
}
