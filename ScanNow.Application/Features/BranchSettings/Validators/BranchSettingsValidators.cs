using FluentValidation;
using ScanNow.Application.Features.BranchSettings.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.BranchSettings.Validators
{
    public class UpsertBranchPaymentConfigRequestValidator : AbstractValidator<UpsertBranchPaymentConfigRequest>
    {
        public UpsertBranchPaymentConfigRequestValidator()
        {
            RuleFor(x => x.DefaultMethod).Must(x => x == PaymentMethod.CASH || x == PaymentMethod.PAYOS);
            RuleFor(x => x.CashEnabled).Equal(true).WithMessage("Cash payment must remain enabled.");
            RuleFor(x => x.PayOsClientId).MaximumLength(200);
            RuleFor(x => x.PayOsApiKey).MaximumLength(500);
            RuleFor(x => x.PayOsChecksumKey).MaximumLength(500);
        }
    }

    public class CreatePaperVoucherRequestValidator : AbstractValidator<CreatePaperVoucherRequest>
    {
        public CreatePaperVoucherRequestValidator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(80);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.DiscountValue).GreaterThan(0);
            RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.MaxDiscountAmount).GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue);
            RuleFor(x => x.ValidUntil)
                .GreaterThan(x => x.ValidFrom)
                .When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue);
        }
    }

    public class UpdatePaperVoucherRequestValidator : AbstractValidator<UpdatePaperVoucherRequest>
    {
        public UpdatePaperVoucherRequestValidator()
        {
            Include(new CreatePaperVoucherRequestValidator());
        }
    }
}
