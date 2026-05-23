using FluentValidation;
using ScanNow.Application.Features.TableQr.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.TableQr.Validators
{
    public class TableQueryValidator : AbstractValidator<TableQuery>
    {
        public TableQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity.HasValue);
            RuleFor(x => x.SortDirection)
                .Must(x => string.IsNullOrWhiteSpace(x) || x.Equals("asc", StringComparison.OrdinalIgnoreCase) || x.Equals("desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("sortDirection only accepts asc or desc");
        }
    }

    public class CreateTableRequestValidator : AbstractValidator<CreateTableRequest>
    {
        public CreateTableRequestValidator()
        {
            RuleFor(x => x.TableNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(50);
        }
    }

    public class UpdateTableRequestValidator : AbstractValidator<UpdateTableRequest>
    {
        public UpdateTableRequestValidator()
        {
            RuleFor(x => x.TableNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(50);
        }
    }

    public class UpdateTableStatusRequestValidator : AbstractValidator<UpdateTableStatusRequest>
    {
        public UpdateTableStatusRequestValidator()
        {
            RuleFor(x => x.Status)
                .Must(x => x is TableStatus.AVAILABLE or TableStatus.RESERVED or TableStatus.DISABLED)
                .WithMessage("status only accepts AVAILABLE, RESERVED or DISABLED");
        }
    }

    public class JoinSessionRequestValidator : AbstractValidator<JoinSessionRequest>
    {
        public JoinSessionRequestValidator()
        {
            RuleFor(x => x.SessionCode)
                .NotEmpty()
                .Length(6)
                .Matches("^[A-Z2-9]{6}$")
                .WithMessage("sessionCode must be 6 uppercase characters");
        }
    }
}
