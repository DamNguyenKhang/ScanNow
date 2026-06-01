using FluentValidation;
using ScanNow.Application.Features.Kitchen.DTOs;

namespace ScanNow.Application.Features.Kitchen.Validators
{
    public class MarkReadyRequestValidator : AbstractValidator<MarkReadyRequest>
    {
        public MarkReadyRequestValidator()
        {
            RuleFor(x => x.OrderItemIds)
                .NotEmpty().WithMessage("OrderItemIds cannot be empty.");

            RuleForEach(x => x.OrderItemIds)
                .NotEmpty().WithMessage("OrderItemId cannot be empty.");
        }
    }
}
