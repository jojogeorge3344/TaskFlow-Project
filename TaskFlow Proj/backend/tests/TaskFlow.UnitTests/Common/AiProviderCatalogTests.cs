using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.UnitTests.Common;

public class AiProviderCatalogTests
{
    [Theory]
    [InlineData(AiProviderKind.Gemini, "gemini-3.8-flash")]
    [InlineData(AiProviderKind.Gemini, "gemini-3.5-flash-lite")]
    [InlineData(AiProviderKind.Gemini, "gemma-3-27b-it")]
    [InlineData(AiProviderKind.Ollama, "llama3.2:3b")]
    [InlineData(AiProviderKind.Ollama, "hf.co/User/Model-GGUF:Q4_K_M")]
    [InlineData(AiProviderKind.OpenAI, "gpt-4o-mini")]
    [InlineData(AiProviderKind.OpenAI, "ft:gpt-4o-mini:my_org:custom:abc123")]
    public void Model_id_passes(AiProviderKind kind, string name) =>
        Assert.True(AiProviderCatalog.IsValidModelId(kind, AiProviderCatalog.NormalizeModelName(kind, name)));

    [Theory]
    [InlineData(AiProviderKind.Gemini, "Gemini_TF_AI")]
    [InlineData(AiProviderKind.Gemini, "Gemini-3.8-Flash")]
    [InlineData(AiProviderKind.Gemini, "-gemini")]
    [InlineData(AiProviderKind.Gemini, "models/")]
    [InlineData(AiProviderKind.Gemini, "tunedModels/my-model")]
    [InlineData(AiProviderKind.OpenAI, "GPT API Key")]
    [InlineData(AiProviderKind.Ollama, "llama 3.2")]
    public void Model_id_fails(AiProviderKind kind, string name) =>
        Assert.False(AiProviderCatalog.IsValidModelId(kind, AiProviderCatalog.NormalizeModelName(kind, name)));

    [Theory]
    [InlineData(AiProviderKind.Gemini, "  models/gemini-3.8-flash ", "gemini-3.8-flash")]
    [InlineData(AiProviderKind.Gemini, "gemini-3.8-flash", "gemini-3.8-flash")]
    [InlineData(AiProviderKind.Ollama, " models/llama3.2:3b ", "models/llama3.2:3b")]
    [InlineData(AiProviderKind.OpenAI, null, "")]
    public void Normalize_trims_and_strips_the_gemini_prefix(AiProviderKind kind, string? name, string expected) =>
        Assert.Equal(expected, AiProviderCatalog.NormalizeModelName(kind, name));

    [Fact]
    public void Gemini_message_names_the_format_and_the_key_label_mistake() =>
        Assert.Equal(
            "A Google Gemini model name is its model ID: lowercase letters, digits, dots and hyphens, for example gemini-3.8-flash. The name you gave your key in Google AI Studio is not a model ID.",
            AiProviderCatalog.ModelIdMessage(AiProviderKind.Gemini));

    [Fact]
    public void Other_providers_message_gives_their_example() =>
        Assert.Equal(
            "A model name is the provider's model ID, without spaces, for example gpt-4o-mini.",
            AiProviderCatalog.ModelIdMessage(AiProviderKind.OpenAI));
}
