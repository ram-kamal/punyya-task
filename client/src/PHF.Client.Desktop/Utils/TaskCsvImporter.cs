using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Media;
using PHF.Core.DTOs;

namespace PHF.Client.Desktop.Utils;

public class ParsedCsvTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedTo { get; set; }
    public string AssignedToName { get; set; } = "Unassigned";
    public string Priority { get; set; } = "MEDIUM";
    public DateTime? DueDate { get; set; }
    public bool DeliverableRequired { get; set; } = true;
    public bool IsSelected { get; set; } = true;
    public string RawStaffHeader { get; set; } = string.Empty;

    public bool IsMatched => AssignedTo.HasValue;
    public string DueDateText => DueDate.HasValue ? DueDate.Value.ToString("yyyy-MM-dd") : "No due date";

    public IBrush MatchBrush => IsMatched
        ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) // Emerald
        : new SolidColorBrush(Color.FromRgb(251, 191, 36)); // Amber
}

public class CsvParseResult
{
    public List<ParsedCsvTask> Tasks { get; set; } = new();
    public string DetectedFormat { get; set; } = "linear";
    public string DetectedFormatLabel { get; set; } = "Standard Linear Format";
    public int TotalFound => Tasks.Count;
    public int UnassignedCount => Tasks.Count(t => !t.AssignedTo.HasValue);
    public int MatchedStaffCount => Tasks.Where(t => t.AssignedTo.HasValue).Select(t => t.AssignedTo!.Value).Distinct().Count();
}

public static class TaskCsvImporter
{
    private static readonly HashSet<string> NonStaffKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "s.no", "sr", "sr no", "date", "day", "item", "task", "title", "project",
        "status", "stauts", "state", "remark", "remarks", "comment", "comments",
        "notes", "priority", "assignee", "assigned to", "staff", "scope",
        "description", "proof", "deliverable"
    };

    public static CsvParseResult Parse(string rawText, IEnumerable<StaffMemberDto> staffList)
    {
        var staff = staffList.ToList();
        var rows = ParseDelimitedRows(rawText);

        if (rows.Count == 0)
        {
            return new CsvParseResult
            {
                DetectedFormat = "unknown",
                DetectedFormatLabel = "Empty or unrecognized CSV text"
            };
        }

        // Find header row
        int headerRowIdx = rows.FindIndex(r => r.Any(c =>
        {
            var l = c.Trim().ToLowerInvariant();
            return l is "item" or "task" or "title" or "project" or "task title" or "project / task title" or "project work";
        }));

        if (headerRowIdx == -1) headerRowIdx = 0;

        var headers = rows[headerRowIdx].Select(h => h.Trim()).ToList();

        // Identify key column indices
        int itemColIdx = headers.FindIndex(h =>
        {
            var l = h.ToLowerInvariant();
            return l is "item" or "task" or "title" or "project" or "task title" or "project / task title" or "project work";
        });

        int dateColIdx = headers.FindIndex(h => h.ToLowerInvariant().Contains("date"));
        int priorityColIdx = headers.FindIndex(h => h.ToLowerInvariant().Contains("priority"));
        int assigneeColIdx = headers.FindIndex(h =>
        {
            var l = h.ToLowerInvariant();
            return l is "assignee" or "assigned to" or "staff" or "assigned staff";
        });
        int descColIdx = headers.FindIndex(h =>
        {
            var l = h.ToLowerInvariant();
            return l is "description" or "scope" or "scope / description" or "responsibility" or "remarks" or "remark";
        });
        int deliverableColIdx = headers.FindIndex(h =>
        {
            var l = h.ToLowerInvariant();
            return l.Contains("proof") || l.Contains("deliverable");
        });

        // Determine if this is a Team Responsibility Matrix
        var candidateStaffCols = headers
            .Select((name, idx) => (name, idx))
            .Where(col =>
            {
                if (col.idx == itemColIdx || col.idx == dateColIdx || col.idx == priorityColIdx) return false;
                var l = col.name.ToLowerInvariant();
                return !NonStaffKeywords.Contains(l) && col.name.Length > 0;
            })
            .ToList();

        bool isMatrix = candidateStaffCols.Count > 0 && assigneeColIdx == -1;
        var resultTasks = new List<ParsedCsvTask>();

        if (isMatrix)
        {
            // Parse Matrix Format
            for (int r = headerRowIdx + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                var projectTitle = itemColIdx >= 0 && itemColIdx < row.Count ? row[itemColIdx].Trim() : "";
                if (string.IsNullOrWhiteSpace(projectTitle)) continue;

                var dateVal = dateColIdx >= 0 && dateColIdx < row.Count ? row[dateColIdx] : null;
                var dueDate = ParseDate(dateVal);

                foreach (var sc in candidateStaffCols)
                {
                    if (sc.idx >= row.Count) continue;
                    var cellScope = row[sc.idx].Trim();
                    if (string.IsNullOrWhiteSpace(cellScope)) continue;

                    var lowerScope = cellScope.ToLowerInvariant();
                    if (lowerScope is "on going" or "ongoing" or "completed" or "done") continue;

                    var matched = MatchStaff(sc.name, staff);

                    resultTasks.Add(new ParsedCsvTask
                    {
                        Title = projectTitle,
                        Description = cellScope,
                        AssignedTo = matched?.Id,
                        AssignedToName = matched?.FullName ?? sc.name,
                        Priority = "MEDIUM",
                        DueDate = dueDate,
                        DeliverableRequired = true,
                        IsSelected = true,
                        RawStaffHeader = sc.name
                    });
                }
            }
        }
        else
        {
            // Parse Standard Linear Format
            for (int r = headerRowIdx + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                var title = (itemColIdx >= 0 && itemColIdx < row.Count ? row[itemColIdx] : (row.Count > 0 ? row[0] : ""))?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(title)) continue;

                var staffStr = (assigneeColIdx >= 0 && assigneeColIdx < row.Count ? row[assigneeColIdx] : "")?.Trim() ?? "";
                var descStr = (descColIdx >= 0 && descColIdx < row.Count ? row[descColIdx] : "")?.Trim() ?? "";
                var priorityStr = (priorityColIdx >= 0 && priorityColIdx < row.Count ? row[priorityColIdx] : "")?.Trim().ToUpperInvariant() ?? "MEDIUM";
                var dateVal = dateColIdx >= 0 && dateColIdx < row.Count ? row[dateColIdx] : null;
                var deliverableVal = (deliverableColIdx >= 0 && deliverableColIdx < row.Count ? row[deliverableColIdx] : "")?.Trim().ToLowerInvariant() ?? "yes";

                var validPriority = priorityStr switch
                {
                    "LOW" or "HIGH" or "CRITICAL" => priorityStr,
                    _ => "MEDIUM"
                };

                var matched = MatchStaff(staffStr, staff);

                resultTasks.Add(new ParsedCsvTask
                {
                    Title = title,
                    Description = descStr,
                    AssignedTo = matched?.Id,
                    AssignedToName = matched?.FullName ?? (string.IsNullOrWhiteSpace(staffStr) ? "Unassigned" : staffStr),
                    Priority = validPriority,
                    DueDate = ParseDate(dateVal),
                    DeliverableRequired = deliverableVal != "no" && deliverableVal != "false",
                    IsSelected = true,
                    RawStaffHeader = staffStr
                });
            }
        }

        return new CsvParseResult
        {
            Tasks = resultTasks,
            DetectedFormat = isMatrix ? "matrix" : "linear",
            DetectedFormatLabel = isMatrix
                ? $"Team Responsibility Matrix ({string.Join(", ", candidateStaffCols.Select(c => c.name))})"
                : "Standard Linear CSV Format"
        };
    }

    private static StaffMemberDto? MatchStaff(string query, List<StaffMemberDto> staff)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var q = query.Trim().ToLowerInvariant();

        // Exact match on username, full name, or email
        var exact = staff.FirstOrDefault(s =>
            (s.Username != null && s.Username.ToLowerInvariant() == q) ||
            s.FullName.ToLowerInvariant() == q ||
            s.Email.ToLowerInvariant() == q);
        if (exact != null) return exact;

        // Partial / substring match
        var partial = staff.FirstOrDefault(s =>
            s.FullName.ToLowerInvariant().Contains(q) ||
            (s.Username != null && s.Username.ToLowerInvariant().Contains(q)) ||
            s.Email.ToLowerInvariant().Contains(q) ||
            q.Contains(s.FullName.ToLowerInvariant()) ||
            (s.Username != null && q.Contains(s.Username.ToLowerInvariant())));
        return partial;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        var clean = dateStr.Trim();

        string[] formats = { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "d-M-yyyy", "d/M/yyyy" };
        if (DateTime.TryParseExact(clean, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt;
        }

        if (DateTime.TryParse(clean, out var fallback))
        {
            return fallback;
        }

        return null;
    }

    private static List<List<string>> ParseDelimitedRows(string text)
    {
        var result = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return result;

        // Detect delimiter: tab vs comma
        var sample = string.Join("\n", lines.Take(5));
        char delimiter = sample.Count(c => c == '\t') > sample.Count(c => c == ',') ? '\t' : ',';

        var currentRow = new List<string>();
        var currentCell = "";
        bool insideQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char nextChar = i + 1 < text.Length ? text[i + 1] : '\0';

            if (c == '"')
            {
                if (insideQuotes && nextChar == '"')
                {
                    currentCell += '"';
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (c == delimiter && !insideQuotes)
            {
                currentRow.Add(currentCell.Trim());
                currentCell = "";
            }
            else if ((c == '\r' || c == '\n') && !insideQuotes)
            {
                if (c == '\r' && nextChar == '\n') i++;
                currentRow.Add(currentCell.Trim());
                if (currentRow.Any(cell => cell.Length > 0))
                {
                    result.Add(currentRow);
                }
                currentRow = new List<string>();
                currentCell = "";
            }
            else
            {
                currentCell += c;
            }
        }

        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.Trim());
            if (currentRow.Any(cell => cell.Length > 0))
            {
                result.Add(currentRow);
            }
        }

        return result;
    }

    public static string GetSampleMatrixCsv()
    {
        return @"No,Date,Item,Ayush,Himanshu,Dhvaj,Divyanshu,Status,Remarks
1,21-09-2026,Dr. Heart Vaccine - Urban Short Film,""Script, Production"",Camera and editing,,,In Progress,
2,21-09-2026,Dr. Heart Vaccine - Rural Short Film,Story Board options,,,,,,
3,21-09-2026,NPPEHA LLM Videos,,,Development,Editing and finishing,In Progress,
4,21-09-2026,SAAOL Community Centre Video,To complete Hindi Narration,Video Editing,,,In Progress,
5,21-09-2026,Dr. Heart Prevention App,,,Development,Content support,In Progress,
6,21-09-2026,Punyya Website,,,Frontend & Backend,Content Support,In Progress,
7,21-09-2026,Dr Heart LLM,,,LLM Integration,Content Support,In Progress,";
    }

    public static string GetSampleLinearCsv()
    {
        return @"Project / Task Title,Assigned Staff,Scope / Description,Priority,Due Date,Proof Required
Dr. Heart Vaccine - Urban Short Film,Ayush,Script writing and production coordination,HIGH,2026-10-10,YES
Dr. Heart Vaccine - Urban Short Film,Himanshu,Camera capture and full 4K video editing,HIGH,2026-10-10,YES
Dr. Heart Prevention App,Dhvaj,Core mobile app development & API sync,CRITICAL,2026-10-15,YES
Dr. Heart Prevention App,Divyanshu,Health content review and UI support,MEDIUM,2026-10-15,YES
Punyya Website,Dhvaj,Production website updates and deployment,HIGH,2026-10-12,YES";
    }
}
