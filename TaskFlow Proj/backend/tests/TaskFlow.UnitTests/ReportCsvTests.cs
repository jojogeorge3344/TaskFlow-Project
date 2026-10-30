using TaskFlow.Api.Services.Reports;
using TaskFlow.Api.Dtos.Reports;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class ReportCsvTests
{
    [Fact]
    public void Write_EmitsTheHeaderRowFirst()
    {
        var csv = ReportCsv.Write([Row(title: "Ordinary task")]);

        var header = csv.Split("\r\n")[0];
        Assert.Equal(
            "TaskCode,Title,Assignee,Project,Priority,Status,StartDate,Deadline,EstimatedHours,CompletedAt",
            header);
    }

    [Fact]
    public void Write_PrefixesCellsThatBeginWithAFormulaCharacter()
    {
        foreach (var dangerous in new[] { "=1+1", "+SUM(A1)", "-2+3", "@cmd" })
        {
            var csv = ReportCsv.Write([Row(title: dangerous)]);

            var dataRow = csv.Split("\r\n")[1];
            Assert.Contains(",'" + dangerous + ",", dataRow);
        }
    }

    [Fact]
    public void Write_QuotesCellsContainingCommasQuotesOrNewlines()
    {
        var csv = ReportCsv.Write([Row(title: "Release, then \"ship\"")]);

        var dataRow = csv.Split("\r\n")[1];
        Assert.Contains("\"Release, then \"\"ship\"\"\"", dataRow);
    }

    [Fact]
    public void Write_RendersDatesAndNumbersInvariantly()
    {
        var csv = ReportCsv.Write([Row(title: "Dated")]);

        var dataRow = csv.Split("\r\n")[1];
        Assert.Contains("2026-06-01", dataRow);
        Assert.Contains("2026-06-15", dataRow);
        Assert.Contains(",8,", dataRow);
    }

    private static ReportRowDto Row(
        string title,
        string project = "",
        DateTime? completedAt = null) =>
        new(
            "TSK-0001",
            title,
            "Alice Adams",
            project,
            TaskPriority.High,
            TaskStatus.ToDo,
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 15),
            8,
            completedAt);
}
