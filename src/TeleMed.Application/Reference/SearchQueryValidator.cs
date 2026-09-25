using FluentValidation;

namespace TeleMed.Application.Reference;

public sealed class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public SearchQueryValidator()
    {
        RuleFor(x => x.Q).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Limit).InclusiveBetween(1, SearchQuery.MaxLimit);
    }
}
