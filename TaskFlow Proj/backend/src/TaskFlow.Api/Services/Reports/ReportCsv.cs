using TaskFlow.Api.Dtos.Reports;
using System.Globalization;
using System.Text;

namespace TaskFlow.Api.Services.Reports;

/// <summary>Serialises report rows to RFC 4180 CSV (Screen 17). Cell values that begin with a
/// spreadsheet formula character are prefixed with a single quote to prevent formula injection (D8).</summary>
public static class ReportCsv
{
    public const string ContentType = "text/csv";
    public const string FileName = "taskflow-report.csv";

    private static readonly string[] Headers =
    [
        "TaskCode",
        "Title",
        "Assignee",
        "Project",
        "Priority",
        "Status",
        "StartDate",
        "Deadline",
        "EstimatedHours",
        "CompletedAt"
    ];

    public static string Write(IEnumerable<ReportRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', Headers)).Append("\r\n");

        foreach (var row in rows)
        {
            var cells = new[]
            {
                Escape(row.TaskCode),
                Escape(row.Title),
                Escape(row.Assignee),
                Escape(row.Project),
                Escape(row.Priority.ToString()),
                Escape(row.Status.ToString()),
                Escape(FormatDate(row.StartDate)),
                Escape(FormatDate(row.Deadline)),
                Escape(row.EstimatedHours.ToString(CultureInfo.InvariantCulture)),
                Escape(row.CompletedAt is null ? string.Empty : FormatDate(row.CompletedAt.Value))
            };

            builder.Append(string.Join(',', cells)).Append("\r\n");
        }

        return builder.ToString();
    }

    private static string FormatDate(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        value ??= string.Empty;

        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
