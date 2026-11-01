using TaskFlow.Api.Dtos.Settings.TfaSettings.Validators;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using FluentValidation.TestHelper;

namespace TaskFlow.UnitTests.Settings.TfaSettings.ProviderConfig;

public class AddAiModelRequestValidatorTests
{
    private readonly AddAiModelRequestValidator _validator = new();

    [Fact]
    public void Model_name_with_key_passes() =>
        _validator.TestValidate(new AddAiModelRequest("gemini-2.5-flash", "AIza-test-key", null)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_model_name_fails(string? name) =>
        _validator.TestValidate(new AddAiModelRequest(name, null, null)).ShouldHaveValidationErrorFor(request => request.ModelName);

    [Theory]
    [InlineData("Gemini API Key")]
    [InlineData("gemini 3.8 flash")]
    [InlineData("gpt-4o\tmini")]
    [InlineData("someone@example.com")]
    public void Model_name_with_spaces_or_an_email_address_fails(string name)
    {
        var result = _validator.TestValidate(new AddAiModelRequest(name, "key", null));

        result.ShouldHaveValidationErrorFor(request => request.ModelName)
            .WithErrorMessage("A model name is the provider's model ID, without spaces or @, for example gemini-3.8-flash.");
    }

    [Theory]
    [InlineData("gemini-3.8-flash")]
    [InlineData("llama3.2:3b")]
    [InlineData("  gpt-4o-mini  ")]
    public void Model_id_passes(string name) =>
        _validator.TestValidate(new AddAiModelRequest(name, "key", null)).ShouldNotHaveValidationErrorFor(request => request.ModelName);

    [Fact]
    public void Model_name_at_the_100_character_limit_passes() =>
        _validator.TestValidate(new AddAiModelRequest(new string('m', 100), null, null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Model_name_over_the_100_character_limit_fails() =>
        _validator.TestValidate(new AddAiModelRequest(new string('m', 101), null, null)).ShouldHaveValidationErrorFor(request => request.ModelName);

    [Fact]
    public void Key_with_a_space_fails() =>
        _validator.TestValidate(new AddAiModelRequest("gpt-4o-mini", "sk abc", null)).ShouldHaveValidationErrorFor(request => request.ApiKey);

    [Fact]
    public void Key_over_500_characters_fails() =>
        _validator.TestValidate(new AddAiModelRequest("gpt-4o-mini", new string('k', 501), null)).ShouldHaveValidationErrorFor(request => request.ApiKey);

    [Theory]
    [InlineData("http://localhost:11434")]
    [InlineData("https://ollama.internal.example")]
    public void Http_server_address_passes(string url) =>
        _validator.TestValidate(new AddAiModelRequest("llama3.2:3b", null, url)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("ftp://localhost")]
    [InlineData("not a url")]
    [InlineData("http://169.254.169.254")]
    [InlineData("http://metadata.google.internal")]
    public void Unsafe_or_invalid_server_address_fails(string url) =>
        _validator.TestValidate(new AddAiModelRequest("llama3.2:3b", null, url)).ShouldHaveValidationErrorFor(request => request.BaseUrl);

    [Fact]
    public void A_new_key_and_clear_key_together_fail() =>
        Assert.Contains(
            _validator.TestValidate(new AddAiModelRequest("gemini-2.5-pro", "AIza-new", null, ClearKey: true)).Errors,
            error => error.ErrorMessage == "Provide a new key or clear the key, not both.");

    [Fact]
    public void Clear_key_alone_passes() =>
        _validator.TestValidate(new AddAiModelRequest("gemini-2.5-pro", null, null, ClearKey: true)).ShouldNotHaveAnyValidationErrors();
}
