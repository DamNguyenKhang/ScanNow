using FluentValidation;
using ScanNow.Application.Features.Waiter.DTOs;

namespace ScanNow.Application.Features.Waiter.Validators
{
    public class MarkItemsServedRequestValidator : AbstractValidator<MarkItemsServedRequest>
    {
        public MarkItemsServedRequestValidator()
        {
            RuleFor(x => x.OrderItemIds)
                .NotEmpty().WithMessage("OrderItemIds cannot be empty.");

            RuleForEach(x => x.OrderItemIds)
                .NotEmpty().WithMessage("OrderItemId cannot be empty.");
        }
    }

    public class CreateWaiterOrderRequestValidator : AbstractValidator<CreateWaiterOrderRequest>
    {
        public CreateWaiterOrderRequestValidator()
        {
            RuleFor(x => x.TableId)
                .NotEmpty().WithMessage("TableId is required.");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("Order must contain at least one item.");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.MenuItemId)
                    .NotEmpty().WithMessage("MenuItemId is required.");

                item.RuleFor(x => x.Quantity)
                    .GreaterThanOrEqualTo(1).WithMessage("Quantity must be at least 1.");
            });

            RuleFor(x => x.CustomerName)
                .MaximumLength(150).When(x => x.CustomerName != null);

            RuleFor(x => x.CustomerNote)
                .MaximumLength(500).When(x => x.CustomerNote != null);
        }
    }
}
