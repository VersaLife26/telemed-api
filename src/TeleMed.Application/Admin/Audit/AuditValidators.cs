using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Audit;

public sealed class AuditFilterValidator : AbstractValidator<IAuditQuery>
{
    public AuditFilterValidator()
    {
        RuleFor(x => x.EntityType).MaximumLength(100);
        RuleFor(x => x.EntityId).MaximumLength(100);
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        Include(new PageQueryValidator());
        Include(new AuditFilterValidator());
    }
}

public sealed class AuditExportQueryValidator : AbstractValidator<AuditExportQuery>
{
    public AuditExportQueryValidator()
    {
        Include(new AuditFilterValidator());
    }
}
