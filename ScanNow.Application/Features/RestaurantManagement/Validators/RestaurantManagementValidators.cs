using FluentValidation;
using ScanNow.Application.Features.RestaurantManagement.DTOs;

namespace ScanNow.Application.Features.RestaurantManagement.Validators
{
    public class CreateRestaurantRequestValidator : AbstractValidator<CreateRestaurantRequest>
    {
        public CreateRestaurantRequestValidator()
        {
            RuleFor(x => x.OwnerId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(200);
            RuleFor(x => x.LogoUrl).MaximumLength(1000);
        }
    }

    public class UpdateRestaurantRequestValidator : AbstractValidator<UpdateRestaurantRequest>
    {
        public UpdateRestaurantRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(200);
            RuleFor(x => x.LogoUrl).MaximumLength(1000);
        }
    }

    public class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
    {
        public CreateBranchRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(500);
            RuleFor(x => x.Phone).MaximumLength(50);
            RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
            RuleFor(x => x.VatPercent).GreaterThanOrEqualTo(0);
            RuleFor(x => x.ServiceChargePercent).GreaterThanOrEqualTo(0);
            RuleFor(x => x.ServiceChargeFixed).GreaterThanOrEqualTo(0);
        }
    }

    public class UpdateBranchRequestValidator : AbstractValidator<UpdateBranchRequest>
    {
        public UpdateBranchRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(500);
            RuleFor(x => x.Phone).MaximumLength(50);
            RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
            RuleFor(x => x.VatPercent).GreaterThanOrEqualTo(0);
            RuleFor(x => x.ServiceChargePercent).GreaterThanOrEqualTo(0);
            RuleFor(x => x.ServiceChargeFixed).GreaterThanOrEqualTo(0);
        }
    }

    public class RestaurantQueryValidator : AbstractValidator<RestaurantQuery>
    {
        public RestaurantQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.SortDirection)
                .Must(x => string.IsNullOrWhiteSpace(x) || x.Equals("asc", StringComparison.OrdinalIgnoreCase) || x.Equals("desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("sortDirection only accepts asc or desc");
        }
    }

    public class BranchQueryValidator : AbstractValidator<BranchQuery>
    {
        public BranchQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.SortDirection)
                .Must(x => string.IsNullOrWhiteSpace(x) || x.Equals("asc", StringComparison.OrdinalIgnoreCase) || x.Equals("desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("sortDirection only accepts asc or desc");
        }
    }
}
