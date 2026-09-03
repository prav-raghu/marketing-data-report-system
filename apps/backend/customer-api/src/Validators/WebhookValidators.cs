using FluentValidation;
using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Validators;

public sealed class CreateWebhookSubscriptionValidator : AbstractValidator<CreateWebhookSubscriptionDto>
{
    public CreateWebhookSubscriptionValidator()
    {
        RuleFor(x => x.Url).NotEmpty().Must(url => Uri.TryCreate(url, UriKind.Absolute, out _)).WithMessage("Must be a valid URL");
        RuleFor(x => x.Secret).MinimumLength(32).When(x => !string.IsNullOrEmpty(x.Secret));
        RuleFor(x => x.Events).NotEmpty();
        RuleFor(x => x.RetryCount).InclusiveBetween(0, 10).When(x => x.RetryCount.HasValue);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(5, 300).When(x => x.TimeoutSeconds.HasValue);
    }
}

public sealed class UpdateWebhookSubscriptionValidator : AbstractValidator<UpdateWebhookSubscriptionDto>
{
    public UpdateWebhookSubscriptionValidator()
    {
        RuleFor(x => x.Url)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .When(x => x.Url is not null)
            .WithMessage("Must be a valid URL");
        RuleFor(x => x.Secret).MinimumLength(32).When(x => !string.IsNullOrEmpty(x.Secret));
        RuleFor(x => x.RetryCount).InclusiveBetween(0, 10).When(x => x.RetryCount.HasValue);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(5, 300).When(x => x.TimeoutSeconds.HasValue);
    }
}
