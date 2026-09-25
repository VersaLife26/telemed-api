using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Analytics;

public sealed class RevenueQueryValidator : AbstractValidator<RevenueQuery>
{
    public RevenueQueryValidator()
    {
        Include(new DateRangeValidator());
        RuleFor(x => x.Granularity).IsInEnum();
    }
}

public sealed class TopDoctorsQueryValidator : AbstractValidator<TopDoctorsQuery>
{
    public TopDoctorsQueryValidator()
    {
        Include(new DateRangeValidator());
        RuleFor(x => x.Limit).InclusiveBetween(1, TopDoctorsQuery.MaxLimit);
    }
}
