using AdminApi.Dtos;
using FluentValidation;

namespace AdminApi.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class VerifyLoginMfaRequestValidator : AbstractValidator<VerifyLoginMfaRequestDto>
{
    public VerifyLoginMfaRequestValidator()
    {
        RuleFor(x => x.MfaToken).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.ConfirmPassword).MinimumLength(8).MaximumLength(128).When(x => x.ConfirmPassword is not null);
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequestDto>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(2048);
    }
}
