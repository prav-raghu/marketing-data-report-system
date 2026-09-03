using AdminApi.Dtos;
using FluentValidation;

namespace AdminApi.Validators;

public sealed class OnboardingRequestValidator : AbstractValidator<OnboardingRequestDto>
{
    public OnboardingRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(30);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Age).InclusiveBetween(0, 120).When(x => x.Age.HasValue);
        RuleFor(x => x.AllowEmailCommunications).NotNull();
        RuleFor(x => x.IpAddress).NotNull();
        RuleFor(x => x.UserStatusId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class GetUsersPagedRequestValidator : AbstractValidator<GetUsersPagedRequestDto>
{
    public GetUsersPagedRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.SortBy).Must(value => value is "username" or "email" or "createdAt")
            .When(x => x.SortBy is not null);
        RuleFor(x => x.SortOrder).Must(value => value is "asc" or "desc")
            .When(x => x.SortOrder is not null);
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequestDto>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(30);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Avatar).MaximumLength(500).When(x => x.Avatar is not null);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.ConfirmPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class Verify2FARequestValidator : AbstractValidator<Verify2FARequestDto>
{
    public Verify2FARequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Matches("^[0-9]{6}$");
    }
}

public sealed class Disable2FARequestValidator : AbstractValidator<Disable2FARequestDto>
{
    public Disable2FARequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Matches("^[0-9]{6}$");
    }
}
