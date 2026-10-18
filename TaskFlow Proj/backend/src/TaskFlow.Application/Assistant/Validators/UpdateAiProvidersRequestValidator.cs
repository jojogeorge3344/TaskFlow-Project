using FluentValidation;

namespace TaskFlow.Application.Assistant.Validators;

public class UpdateAiProvidersRequestValidator : AbstractValidator<UpdateAiProvidersRequest>
{
    public UpdateAiProvidersRequestValidator()
    {
        RuleFor(request => request.Providers)
            .NotNull().WithMessage("Providers are required.")
            .Must(providers => providers is null || providers.Select(p => p.Kind).Distinct().Count() == providers.Count)
            .WithMessage("Each provider can only appear once.");

        RuleForEach(request => request.Providers).SetValidator(new UpdateAiProviderRequestValidator());
    }
}

public class UpdateAiProviderRequestValidator : AbstractValidator<UpdateAiProviderRequest>
{
    public UpdateAiProviderRequestValidator()
    {
        RuleFor(provider => provider.Kind).NotNull().IsInEnum();
        RuleFor(provider => provider.IsEnabled).NotNull();

        RuleFor(provider => provider.ModelName)
            .NotEmpty().WithMessage("Model name cannot be blank.")
            .MaximumLength(100);

        RuleFor(provider => provider.BaseUrl)
            .Must(BeHttpUrl).WithMessage("The server address must be a valid http or https URL.")
            .MaximumLength(300);

        RuleFor(provider => provider.ApiKey)
            .MaximumLength(500)
            .Must(key => string.IsNullOrEmpty(key) || !key.Any(char.IsWhiteSpace))
            .WithMessage("The API key cannot contain spaces.");

        RuleFor(provider => provider)
            .Must(provider => !(provider.ClearKey == true && !string.IsNullOrEmpty(provider.ApiKey)))
            .WithMessage("Provide a new key or clear the key, not both.");
    }

    private static bool BeHttpUrl(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
}
