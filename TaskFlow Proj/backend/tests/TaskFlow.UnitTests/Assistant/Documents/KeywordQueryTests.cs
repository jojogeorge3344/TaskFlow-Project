using TaskFlow.Api.Services.Assistant.Documents.Rules;

namespace TaskFlow.UnitTests.Assistant.Documents;

public class KeywordQueryTests
{
    [Fact]
    public void StopWordsAndShortTermsAreDropped()
    {
        var query = KeywordQuery.Build("What validations apply when I create a task?");

        Assert.Equal(["validations", "apply", "create", "task"], query.Terms);
        Assert.Equal("\"validations\" \"apply\" \"create\" \"task\"", query.FtsQuery);
        Assert.False(query.HasExactIdentifier);
    }

    [Theory]
    [InlineData("What does NFR-14 say?", "NFR-14")]
    [InlineData("What is TimeoutSeconds for?", "TimeoutSeconds")]
    [InlineData("Is gemini-3.8-flash the default?", "gemini-3.8-flash")]
    [InlineData("Where is the FRD?", "FRD")]
    [InlineData("What is AiProviders__GeminiApiKey", "AiProviders__GeminiApiKey")]
    public void AnExactIdentifierIsKeptWholeAndReported(string message, string identifier)
    {
        var query = KeywordQuery.Build(message);

        Assert.Contains(identifier, query.Terms);
        Assert.Contains($"\"{identifier}\"", query.FtsQuery);
        Assert.True(query.HasExactIdentifier);
    }

    [Fact]
    public void TrailingPunctuationIsTrimmedFromTerms()
    {
        Assert.Equal(["default", "model", "gemini-3.8-flash"], KeywordQuery.Build("default model: gemini-3.8-flash.").Terms);
    }

    [Fact]
    public void FtsOperatorsBecomePlainWords()
    {
        var query = KeywordQuery.Build("what is \"AND\" OR NOT NEAR(task) title:secret prefix* ^start");

        Assert.Equal("\"NEAR\" \"task\" \"title\" \"secret\" \"prefix\" \"start\"", query.FtsQuery);
    }

    [Fact]
    public void InnerQuotesCannotEscapeTheTerm()
    {
        Assert.DoesNotContain("\"\"\"", KeywordQuery.Build("say \"hello\" world").FtsQuery);
    }

    [Fact]
    public void RepeatedTermsAreKeptOnce()
    {
        Assert.Equal(["Task", "tasks"], KeywordQuery.Build("Task task TASK tasks").Terms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("what is it?")]
    [InlineData("?? !! ...")]
    public void AMessageWithNoSignificantTermGivesNoQuery(string? message)
    {
        var query = KeywordQuery.Build(message);

        Assert.Null(query.FtsQuery);
        Assert.False(query.HasExactIdentifier);
    }

    [Theory]
    [InlineData("task", false)]
    [InlineData("Task", false)]
    [InlineData("NFR-14", true)]
    [InlineData("v2", true)]
    [InlineData("snake_case", true)]
    [InlineData("camelCase", true)]
    public void IdentifierRule(string term, bool expected)
    {
        Assert.Equal(expected, KeywordQuery.IsExactIdentifier(term));
    }
}
