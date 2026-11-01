using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.UnitTests.Common;

public class AiProviderCatalogTests
{
    [Theory]
    [InlineData(AiApiType.Gemini, "gemini-3.8-flash")]
    [InlineData(AiApiType.Gemini, "gemini-3.5-flash-lite")]
    [InlineData(AiApiType.Gemini, "gemma-3-27b-it")]
    [InlineData(AiApiType.Ollama, "llama3.2:3b")]
    [InlineData(AiApiType.Ollama, "hf.co/User/Model-GGUF:Q4_K_M")]
    [InlineData(AiApiType.OpenAI, "gpt-4o-mini")]
    [InlineData(AiApiType.OpenAI, "ft:gpt-4o-mini:my_org:custom:abc123")]
    public void Model_id_passes(AiApiType kind, string name) =>
        Assert.True(AiProviderCatalog.IsValidModelId(kind, AiProviderCatalog.NormalizeModelName(kind, name)));

    [Theory]
    [InlineData(AiApiType.Gemini, "Gemini_TF_AI")]
    [InlineData(AiApiType.Gemini, "Gemini-3.8-Flash")]
    [InlineData(AiApiType.Gemini, "-gemini")]
    [InlineData(AiApiType.Gemini, "models/")]
    [InlineData(AiApiType.Gemini, "tunedModels/my-model")]
    [InlineData(AiApiType.OpenAI, "GPT API Key")]
    [InlineData(AiApiType.Ollama, "llama 3.2")]
    [InlineData(AiApiType.OpenAI, "someone@example.com")]
    [InlineData(AiApiType.Ollama, "someone@example.com")]
    public void Model_id_fails(AiApiType kind, string name) =>
        Assert.False(AiProviderCatalog.IsValidModelId(kind, AiProviderCatalog.NormalizeModelName(kind, name)));

    [Theory]
    [InlineData(AiApiType.Gemini, "  models/gemini-3.8-flash ", "gemini-3.8-flash")]
    [InlineData(AiApiType.Gemini, "gemini-3.8-flash", "gemini-3.8-flash")]
    [InlineData(AiApiType.Ollama, " models/llama3.2:3b ", "models/llama3.2:3b")]
    [InlineData(AiApiType.OpenAI, null, "")]
    public void Normalize_trims_and_strips_the_gemini_prefix(AiApiType kind, string? name, string expected) =>
        Assert.Equal(expected, AiProviderCatalog.NormalizeModelName(kind, name));

    [Fact]
    public void Gemini_message_names_the_format_and_the_key_label_mistake() =>
        Assert.Equal(
            "A Google Gemini model name is its model ID: lowercase letters, digits, dots and hyphens, for example gemini-3.8-flash. The name you gave your key in Google AI Studio is not a model ID.",
            AiProviderCatalog.ModelIdMessage(AiApiType.Gemini));

    [Fact]
    public void Other_providers_message_gives_their_example() =>
        Assert.Equal(
            "A model name is the provider's model ID, without spaces or @, for example gpt-4o-mini.",
            AiProviderCatalog.ModelIdMessage(AiApiType.OpenAI));
}
