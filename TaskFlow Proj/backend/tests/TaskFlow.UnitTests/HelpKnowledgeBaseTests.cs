using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Services.Assistant;

namespace TaskFlow.UnitTests;

public class HelpKnowledgeBaseTests
{
    private static readonly HelpKnowledgeBase Help = new();

    [Fact]
    public void ShipsTheExpectedTopics()
    {
        var ids = Help.Search("change status", "Administrator", 20).Select(topic => topic.Id).ToList();

        Assert.Contains("change-task-status", ids);
    }

    [Fact]
    public void StatusChangeHelpExplainsTheStepsAndLinksToTasks()
    {
        var topic = Help.Search("How do I change a task's status?", "Employee").First();

        Assert.Equal("change-task-status", topic.Id);
        Assert.False(topic.IsRestricted);
        Assert.Contains("Move to", topic.Content);
        Assert.Equal("/tasks", topic.Link);
    }

    [Theory]
    [InlineData("how do I send an email to someone", "compose-mail")]
    [InlineData("how to edit my profile", "edit-profile")]
    [InlineData("how do I change my password", "change-password")]
    [InlineData("where are my notifications", "notifications")]
    [InlineData("what can you do", "about-tfa")]
    public void CommonQuestionsFindTheirTopic(string question, string expectedId)
    {
        var top = Help.Search(question, "Employee").First();

        Assert.Equal(expectedId, top.Id);
    }

    [Fact]
    public void AnEmployeeAskingAboutDepartmentsOnlyLearnsWhoManagesThem()
    {
        var topic = Help.Search("How do I create a department?", "Employee").First();

        Assert.Equal("department-management", topic.Id);
        Assert.True(topic.IsRestricted);
        Assert.Contains("Administrator", topic.Content);
        Assert.DoesNotContain("Add", topic.Content);
        Assert.DoesNotContain("1.", topic.Content);
        Assert.Null(topic.Link);
    }

    [Fact]
    public void AnAdministratorGetsTheDepartmentSteps()
    {
        var topic = Help.Search("How do I create a department?", "Administrator").First();

        Assert.False(topic.IsRestricted);
        Assert.Contains("Project Management", topic.Content);
        Assert.Equal("/departments", topic.Link);
    }

    [Fact]
    public void AnEmployeeAskingForARoleChangeIsToldOnlyAnAdministratorCanDoIt()
    {
        var topic = Help.Search("how do I change my role to manager", "Employee").First();

        Assert.Equal("change-role", topic.Id);
        Assert.True(topic.IsRestricted);
        Assert.Contains("authorized administrator", topic.Content);
    }

    [Fact]
    public void ManagerOnlyTopicsAreRestrictedForEmployees()
    {
        var forEmployee = Help.Search("how do I create a new task", "Employee").First();
        var forManager = Help.Search("how do I create a new task", "Manager").First();

        Assert.True(forEmployee.IsRestricted);
        Assert.False(forManager.IsRestricted);
        Assert.Contains("New task", forManager.Content);
    }

    [Theory]
    [InlineData("what is the weather in Paris")]
    [InlineData("tell me a joke")]
    [InlineData("quantum physics lecture notes")]
    [InlineData("")]
    [InlineData("   ")]
    public void UnknownTopicsReturnNothing(string question)
    {
        Assert.Empty(Help.Search(question, "Employee"));
    }

    [Fact]
    public void PasswordHelpNeverAsksForAPasswordInChat()
    {
        var topic = Help.Search("change my password", "Employee").First();

        Assert.Contains("do not type your password into this chat", topic.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/profile", topic.Link);
    }

    [Fact]
    public void ResultsAreLimited()
    {
        Assert.True(Help.Search("task status mail profile password", "Administrator", 2).Count <= 2);
    }
}
