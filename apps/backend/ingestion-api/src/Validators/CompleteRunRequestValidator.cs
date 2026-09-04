using FluentValidation;
using IngestionApi.Dtos;

namespace IngestionApi.Validators;

public sealed class CompleteRunRequestValidator : AbstractValidator<CompleteRunRequestDto>
{
    public CompleteRunRequestValidator()
    {
        RuleFor(x => x.RecordCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PartCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CompressedBytes).GreaterThanOrEqualTo(0);
    }
}
