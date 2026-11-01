namespace TaskFlow.Api.Models.Enums;

/// <summary>The AI providers TFA can use. Ollama runs locally; Gemini and OpenAI are cloud services.</summary>
public enum AiApiType
{
    Ollama,
    Gemini,
    OpenAI
}
