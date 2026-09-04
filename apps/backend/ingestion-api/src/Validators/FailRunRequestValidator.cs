using FluentValidation;
using IngestionApi.Dtos;

namespace IngestionApi.Validators;

public sealed class FailRunRequestValidator : AbstractValidator<FailRunRequestDto>
{
    public FailRunRequestValidator()
    {
        RuleFor(x => x.ErrorCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ErrorMessage).NotEmpty().MaximumLength(2000);
    }
}
