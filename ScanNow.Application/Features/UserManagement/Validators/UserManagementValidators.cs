using FluentValidation;
using ScanNow.Application.Features.UserManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.UserManagement.Validators
{
    public class UserListQueryValidator : AbstractValidator<UserListQuery>
    {
        public UserListQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.SortDirection)
                .Must(x => string.IsNullOrWhiteSpace(x) || x.Equals("asc", StringComparison.OrdinalIgnoreCase) || x.Equals("desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("sortDirection only accepts asc or desc");
        }
    }

    public class CreateOwnerRequestValidator : AbstractValidator<CreateOwnerRequest>
    {
        public CreateOwnerRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.PhoneNumber).MaximumLength(50);
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class UpdateOwnerRequestValidator : AbstractValidator<UpdateOwnerRequest>
    {
        public UpdateOwnerRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.PhoneNumber).MaximumLength(50);
            RuleFor(x => x.Password).MinimumLength(6).When(x => !string.IsNullOrWhiteSpace(x.Password));
        }
    }

    public class CreateManagedUserRequestValidator : AbstractValidator<CreateManagedUserRequest>
    {
        public CreateManagedUserRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.PhoneNumber).MaximumLength(50);
            RuleFor(x => x.Password).NotEmpty();
            RuleFor(x => x.Role).NotEmpty().Must(BeManagedRole).WithMessage("Invalid role");
            RuleFor(x => x.BranchIds)
                .Must(ManagedUserValidationRules.HaveExactlyOneBranch)
                .WithMessage("User must belong to exactly one branch");
        }

        private static bool BeManagedRole(string role)
        {
            return role == UserRole.BRANCH_MANAGER.ToString()
                || role == UserRole.STAFF.ToString()
                || role == UserRole.KITCHEN.ToString()
                || role == UserRole.CASHIER.ToString();
        }
    }

    public class UpdateManagedUserRequestValidator : AbstractValidator<UpdateManagedUserRequest>
    {
        public UpdateManagedUserRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.PhoneNumber).MaximumLength(50);
            RuleFor(x => x.Role).NotEmpty().Must(BeManagedRole).WithMessage("Invalid role");
            RuleFor(x => x.BranchIds)
                .Must(ManagedUserValidationRules.HaveExactlyOneBranch)
                .WithMessage("User must belong to exactly one branch");
        }

        private static bool BeManagedRole(string role)
        {
            return role == UserRole.BRANCH_MANAGER.ToString()
                || role == UserRole.STAFF.ToString()
                || role == UserRole.KITCHEN.ToString()
                || role == UserRole.CASHIER.ToString();
        }
    }

    internal static class ManagedUserValidationRules
    {
        public static bool HaveExactlyOneBranch(List<Guid>? branchIds)
        {
            return branchIds is not null
                && branchIds.Count == 1
                && branchIds[0] != Guid.Empty;
        }
    }
}
