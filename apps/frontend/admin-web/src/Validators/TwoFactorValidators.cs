using AdminWeb.Models;
using FluentValidation;

namespace AdminWeb.Validators;

public sealed class Verify2FARequestValidator : AbstractValidator<Verify2FARequest>
{
    public Verify2FARequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Matches("^[0-9]{6}$");
    }
}

public sealed class Disable2FARequestValidator : AbstractValidator<Disable2FARequest>
{
    public Disable2FARequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Matches("^[0-9]{6}$");
    }
}
