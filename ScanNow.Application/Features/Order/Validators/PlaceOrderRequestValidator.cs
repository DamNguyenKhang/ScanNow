using FluentValidation;
using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Application.Features.Order.Validators
{
    public class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
    {
        public PlaceOrderRequestValidator()
        {
            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("Order must contain at least one item.");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.MenuItemId)
                    .NotEmpty().WithMessage("MenuItemId is required.");

                item.RuleFor(x => x.Quantity)
                    .GreaterThanOrEqualTo(1).WithMessage("Quantity must be at least 1.");

                item.RuleFor(x => x.SpecialRequest)
                    .MaximumLength(300).When(x => x.SpecialRequest != null);
            });

            RuleFor(x => x.CustomerName)
                .MaximumLength(150).When(x => x.CustomerName != null);

            RuleFor(x => x.CustomerPhone)
                .MaximumLength(20).When(x => x.CustomerPhone != null);

            RuleFor(x => x.CustomerNote)
                .MaximumLength(500).When(x => x.CustomerNote != null);
        }
    }
}
