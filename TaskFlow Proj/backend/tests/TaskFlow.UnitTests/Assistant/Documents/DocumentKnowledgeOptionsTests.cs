using TaskFlow.Api.Config;

namespace TaskFlow.UnitTests.Assistant.Documents;

public class DocumentKnowledgeOptionsTests
{
    [Fact]
    public void TheDefaultSourcePathIsTheWorkspaceDocumentsFolder()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "workspace");
        var contentRoot = Path.Combine(workspace, "TaskFlow Proj", "backend", "src", "TaskFlow.Api");

        var resolved = new DocumentKnowledgeOptions().ResolveSourcePath(contentRoot);

        Assert.Equal(Path.GetFullPath(Path.Combine(workspace, "TF - Documents")), resolved);
    }

    [Fact]
    public void TheDefaultSourcePathFindsTheRealDocumentsFolderFromTheApiProject()
    {
        var resolved = new DocumentKnowledgeOptions().ResolveSourcePath(ApiProjectFolder());

        Assert.Equal("TF - Documents", Path.GetFileName(resolved));
        Assert.True(Directory.Exists(resolved), $"Expected the documents folder at {resolved}.");
    }

    [Fact]
    public void AnAbsoluteSourcePathIsKept()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "docs");

        var resolved = new DocumentKnowledgeOptions { SourcePath = absolute }.ResolveSourcePath(Path.Combine(Path.GetTempPath(), "api"));

        Assert.Equal(Path.GetFullPath(absolute), resolved);
    }

    [Fact]
    public void TheDefaultsAreValid()
    {
        Assert.Empty(new DocumentKnowledgeOptions().Validate());
    }

    [Theory]
    [InlineData(nameof(DocumentKnowledgeOptions.TopK), 0)]
    [InlineData(nameof(DocumentKnowledgeOptions.TopK), 21)]
    [InlineData(nameof(DocumentKnowledgeOptions.MinScore), -0.1)]
    [InlineData(nameof(DocumentKnowledgeOptions.MinScore), 1.1)]
    [InlineData(nameof(DocumentKnowledgeOptions.MaxChunkCharacters), 299)]
    [InlineData(nameof(DocumentKnowledgeOptions.MaxChunkCharacters), 6001)]
    [InlineData(nameof(DocumentKnowledgeOptions.RescanMinutes), 0)]
    [InlineData(nameof(DocumentKnowledgeOptions.EmbeddingDimensions), 127)]
    [InlineData(nameof(DocumentKnowledgeOptions.EmbeddingDimensions), 3073)]
    public void AnOutOfRangeValueIsRejectedByName(string key, double value)
    {
        var options = new DocumentKnowledgeOptions();
        var property = typeof(DocumentKnowledgeOptions).GetProperty(key)!;
        property.SetValue(options, Convert.ChangeType(value, property.PropertyType));

        var problems = options.Validate();

        Assert.Contains(problems, problem => problem.Contains($"DocumentKnowledge:{key}"));
    }

    [Fact]
    public void TheDefaultEmbeddingIsGeminiAndTheStoredModelNamesItsSize()
    {
        var options = new DocumentKnowledgeOptions();

        Assert.Equal(("gemini-embedding-2", 768, "gemini-embedding-2@768"), (options.EmbeddingModel, options.EmbeddingDimensions, options.IndexModel));
    }

    [Fact]
    public void ATooLongEmbeddingModelIsRejected()
    {
        var problems = new DocumentKnowledgeOptions { EmbeddingModel = new string('m', 91) }.Validate();

        Assert.Contains(problems, problem => problem.Contains("DocumentKnowledge:EmbeddingModel"));
    }

    [Fact]
    public void TheValidationNamesTheProblem()
    {
        var result = new DocumentKnowledgeOptionsValidation().Validate(null, new DocumentKnowledgeOptions { TopK = 0 });

        Assert.True(result.Failed);
        Assert.Contains("DocumentKnowledge:TopK", result.FailureMessage);
    }

    private static string ApiProjectFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TaskFlow.sln")))
            {
                return Path.Combine(directory.FullName, "src", "TaskFlow.Api");
            }
        }

        throw new InvalidOperationException("TaskFlow.sln was not found above the test output folder.");
    }
}
