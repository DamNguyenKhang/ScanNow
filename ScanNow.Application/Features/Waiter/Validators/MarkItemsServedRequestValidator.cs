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
}
