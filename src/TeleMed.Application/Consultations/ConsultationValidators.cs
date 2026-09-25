using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Consultations;

public sealed class EndConsultationRequestValidator : AbstractValidator<EndConsultationRequest>
{
    public EndConsultationRequestValidator() => RuleFor(x => x.Reason).MaximumLength(200);
}

public sealed class QualityReportRequestValidator : AbstractValidator<QualityReportRequest>
{
    public QualityReportRequestValidator()
    {
        RuleFor(x => x.Quality).IsInEnum();
        RuleFor(x => x.PacketLossPct).InclusiveBetween(0, 100);
        RuleFor(x => x.BitrateKbps).GreaterThanOrEqualTo(0);
    }
}

public sealed class PostMessageRequestValidator : AbstractValidator<PostMessageRequest>
{
    public PostMessageRequestValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(ConsultationMessage.MaxBodyLength);
}

public sealed class ConsultationMessageQueryValidator : AbstractValidator<ConsultationMessageQuery>
{
    public ConsultationMessageQueryValidator() => Include(new PageQueryValidator());
}
