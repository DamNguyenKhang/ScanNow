using FluentValidation;
using ScanNow.Application.Features.MenuManagement.DTOs;

namespace ScanNow.Application.Features.MenuManagement.Validators
{
    public class CategoryQueryValidator : AbstractValidator<CategoryQuery>
    {
        public CategoryQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.SortDirection).Must(BeSortDirection).WithMessage("sortDirection only accepts asc or desc");
        }

        private static bool BeSortDirection(string? value) => string.IsNullOrWhiteSpace(value)
            || value.Equals("asc", StringComparison.OrdinalIgnoreCase)
            || value.Equals("desc", StringComparison.OrdinalIgnoreCase);
    }

    public class MenuQueryValidator : AbstractValidator<MenuQuery>
    {
        public MenuQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.SortDirection).Must(BeSortDirection).WithMessage("sortDirection only accepts asc or desc");
        }

        private static bool BeSortDirection(string? value) => string.IsNullOrWhiteSpace(value)
            || value.Equals("asc", StringComparison.OrdinalIgnoreCase)
            || value.Equals("desc", StringComparison.OrdinalIgnoreCase);
    }

    public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
    {
        public CreateCategoryRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ImageUrl).MaximumLength(1000);
        }
    }

    public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
    {
        public UpdateCategoryRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ImageUrl).MaximumLength(1000);
        }
    }

    public class CreateMenuItemRequestValidator : AbstractValidator<CreateMenuItemRequest>
    {
        public CreateMenuItemRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ImageUrl).MaximumLength(1000);
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
            RuleFor(x => x.PreparationTime).GreaterThanOrEqualTo(0);
        }
    }

    public class UpdateMenuItemRequestValidator : AbstractValidator<UpdateMenuItemRequest>
    {
        public UpdateMenuItemRequestValidator()
        {
            RuleFor(x => x.CategoryId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ImageUrl).MaximumLength(1000);
            RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
            RuleFor(x => x.PreparationTime).GreaterThanOrEqualTo(0);
        }
    }

    public class UpdateMenuItemPriceRequestValidator : AbstractValidator<UpdateMenuItemPriceRequest>
    {
        public UpdateMenuItemPriceRequestValidator()
        {
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Note).MaximumLength(500);
        }
    }

    public class ReorderCategoryRequestValidator : AbstractValidator<ReorderCategoryRequest>
    {
        public ReorderCategoryRequestValidator()
        {
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).SetValidator(new ReorderItemRequestValidator());
        }
    }

    public class ReorderMenuItemRequestValidator : AbstractValidator<ReorderMenuItemRequest>
    {
        public ReorderMenuItemRequestValidator()
        {
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).SetValidator(new ReorderItemRequestValidator());
        }
    }

    public class ReorderItemRequestValidator : AbstractValidator<ReorderItemRequest>
    {
        public ReorderItemRequestValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        }
    }

    public class BulkAvailabilityRequestValidator : AbstractValidator<BulkAvailabilityRequest>
    {
        public BulkAvailabilityRequestValidator()
        {
            RuleFor(x => x.MenuItemIds).NotEmpty();
        }
    }
}
