using System.Net;
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
            .Must(BeHttpUrl).WithMessage("The server address must be a valid http or https URL, and not a link-local or cloud metadata address.")
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
        || (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !IsMetadataHost(uri));

    /// <summary>The server calls this address, so the addresses cloud hosts use for instance metadata (and other
    /// link-local ones) are refused. A local or private address, such as localhost, stays allowed: that is where Ollama runs.</summary>
    internal static bool IsMetadataHost(Uri uri)
    {
        var host = uri.Host.Trim('[', ']').ToLowerInvariant();
        if (host is "metadata.google.internal" or "metadata" or "instance-data")
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }
}
