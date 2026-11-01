using FluentValidation;

namespace TaskFlow.Api.Dtos.Settings.TfaSettings.Validators;

/// <summary>Checks the Add AI model page's values. Rules that depend on the provider (a key is required when none is
/// saved; Ollama takes no key) are checked by the service, which knows the provider and its saved key.</summary>
public class AddAiModelRequestValidator : AbstractValidator<AddAiModelRequest>
{
    public AddAiModelRequestValidator()
    {
        RuleFor(request => request.ModelName)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Enter the model name.")
            .Must(name => name is null || name.Trim().Length <= 100).WithMessage("The model name can be at most 100 characters.")
            .Must(UpdateAiProviderRequestValidator.BeAModelId).WithMessage(UpdateAiProviderRequestValidator.ModelIdMessage);

        RuleFor(request => request.ApiKey)
            .MaximumLength(500)
            .Must(key => string.IsNullOrEmpty(key) || !key.Any(char.IsWhiteSpace))
            .WithMessage("The API key cannot contain spaces.");

        RuleFor(request => request)
            .Must(request => !(request.ClearKey == true && !string.IsNullOrEmpty(request.ApiKey)))
            .WithMessage("Provide a new key or clear the key, not both.");

        RuleFor(request => request.BaseUrl)
            .Must(UpdateAiProviderRequestValidator.BeHttpUrl)
            .WithMessage("The server address must be a valid http or https URL, and not a link-local or cloud metadata address.")
            .MaximumLength(300);
    }
}
