using System.Text.Json;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Assistant.Tools;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests.Assistant.Chat;

/// <summary>Spec requirement "Live-data questions keep using the existing tools": retrieved document text does not take
/// the place of the user's own data.</summary>
public class AssistantLiveDataTests
{
    [Fact]
    public async Task AnOverdueTasksQuestionUsesTheTaskToolEvenWhenDocumentsMatch()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        var deadline = DateTime.UtcNow.Date.AddDays(-2).AddHours(12);
        h.Tracking.AddTask(h.Asha.Id, h.Ravi.Id, "Fix the export bug", TaskStatus.InProgress, deadline, TaskPriority.High);
        h.Tracking.AddTask(h.Asha.Id, h.Ravi.Id, "Write the release notes", TaskStatus.InProgress, DateTime.UtcNow.Date.AddDays(5), TaskPriority.Low);
        h.Tracking.AddTask(h.Ravi.Id, h.Ravi.Id, "Ravi's own overdue task", TaskStatus.InProgress, deadline, TaskPriority.High);

        var getMyTasks = new GetMyTasksTool(new AssistantTaskReader(h.Tracking.TaskService, h.Tracking.Clock));
        h.Toolbox.Definitions.Clear();
        h.Toolbox.Definitions.Add(new ChatToolDefinition(getMyTasks.Name, getMyTasks.Description, getMyTasks.ParametersSchema));
        h.Toolbox.Handler = call => getMyTasks
            .ExecuteAsync(AssistantServiceHarness.Caller(h.Asha), 1, JsonDocument.Parse(call.ArgumentsJson).RootElement.Clone(), CancellationToken.None)
            .GetAwaiter().GetResult();

        h.Documents.Result = ScriptedDocumentSearch.Found(
            ("FRD – Task Management", "4.1.1 Screen: Task List", "A task is overdue when its deadline has passed and it is not Completed."));

        // The model asks for the overdue tasks, then answers with the titles the tool returned.
        h.Gateway.Handler = (_, request) =>
        {
            var toolResult = request.Messages.LastOrDefault(message => message.Role == AssistantMessageRole.Tool);
            if (toolResult is null)
            {
                return new ChatModelResult(null, [new ChatToolCall("call_1", "get_my_tasks", """{"filter":"overdue"}""")]);
            }

            using var json = JsonDocument.Parse(toolResult.Content);
            var titles = json.RootElement.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("title").GetString());
            return ChatModelResult.FromText("Your overdue tasks: " + string.Join(", ", titles));
        };

        var reply = await h.SendAsync(h.Asha, "which of my tasks are overdue?");

        var first = h.Gateway.Calls[0].Request;
        Assert.Contains(first.Tools, tool => tool.Name == "get_my_tasks");
        Assert.Contains("A task is overdue when its deadline has passed", first.SystemPrompt);
        Assert.Equal("get_my_tasks", Assert.Single(h.Toolbox.Executed).Name);
        var answer = reply.Messages.Last().Content;
        Assert.Equal("Your overdue tasks: Fix the export bug", answer);
        Assert.DoesNotContain("Ravi", answer);
    }
}
