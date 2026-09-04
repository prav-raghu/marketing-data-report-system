using FluentValidation;
using IngestionApi.Dtos;

namespace IngestionApi.Validators;

public sealed class StartRunRequestValidator : AbstractValidator<StartRunRequestDto>
{
    public StartRunRequestValidator()
    {
        RuleFor(x => x.SourceConnectorId).NotEmpty();

        RuleFor(x => x.WindowEnd)
            .GreaterThanOrEqualTo(x => x.WindowStart!.Value)
            .When(x => x.WindowStart.HasValue && x.WindowEnd.HasValue)
            .WithMessage("windowEnd must be on or after windowStart");

        RuleFor(x => x.WindowEnd)
            .NotNull()
            .When(x => x.WindowStart.HasValue)
            .WithMessage("windowEnd is required when windowStart is supplied");

        RuleFor(x => x.WindowStart)
            .NotNull()
            .When(x => x.WindowEnd.HasValue)
            .WithMessage("windowStart is required when windowEnd is supplied");
    }
}
