using FluentValidation;
using ScanNow.Application.Features.Cashier.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.Cashier.Validators
{
    public class CashierOrderQueryValidator : AbstractValidator<CashierOrderQuery>
    {
        public CashierOrderQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.Status)
                .Must(x => string.IsNullOrWhiteSpace(x)
                           || x.Equals("active", StringComparison.OrdinalIgnoreCase)
                           || x.Equals("paid", StringComparison.OrdinalIgnoreCase)
                           || x.Equals("all", StringComparison.OrdinalIgnoreCase))
                .WithMessage("status only accepts active, paid, or all");
            RuleFor(x => x.SortDirection)
                .Must(x => string.IsNullOrWhiteSpace(x)
                           || x.Equals("asc", StringComparison.OrdinalIgnoreCase)
                           || x.Equals("desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("sortDirection only accepts asc or desc");
        }
    }

    public class CashierCheckoutRequestValidator : AbstractValidator<CashierCheckoutRequest>
    {
        public CashierCheckoutRequestValidator()
        {
            RuleFor(x => x.PaymentMethod)
                .Must(x => x == PaymentMethod.CASH || x == PaymentMethod.PAYOS)
                .WithMessage("Cashier checkout only supports CASH or PAYOS");
            RuleFor(x => x.VoucherCode).MaximumLength(80);
            RuleFor(x => x.AmountReceived)
                .NotNull()
                .When(x => x.PaymentMethod == PaymentMethod.CASH)
                .WithMessage("Amount received is required for cash payment.");
            RuleFor(x => x.AmountReceived)
                .GreaterThan(0)
                .When(x => x.PaymentMethod == PaymentMethod.CASH && x.AmountReceived.HasValue)
                .WithMessage("Amount received must be greater than 0.");
        }
    }
}
