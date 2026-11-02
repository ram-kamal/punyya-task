namespace PHF.Core.Utils;

public static class CsvExportFilter
{
    /// <summary>
    /// Checks whether an employee / user should be excluded from CSV exports.
    /// Excludes:
    /// 1. Ramkamal (matched via FullName, Email, or Username)
    /// 2. Viewers / Monitors (matched via Role: VIEWER or MONITOR)
    /// </summary>
    public static bool ShouldExcludeFromCsv(string? fullName, string? email, string? username = null, string? role = null)
    {
        // Check if role is Viewer / Monitor
        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToUpperInvariant();
            if (r == "VIEWER" || r == "MONITOR")
            {
                return true;
            }
        }

        // Check if name, email, or username refers to Ramkamal
        if (IsRamkamalMatch(fullName) || IsRamkamalMatch(email) || IsRamkamalMatch(username))
        {
            return true;
        }

        return false;
    }

    private static bool IsRamkamalMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Normalize string by stripping common delimiters and punctuation
        var normalized = value.ToLowerInvariant()
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace("@", "")
            .Replace("'", "")
            .Replace("\"", "");

        // Matches: ramkamal, ram kamal, ram.kamal, ram-kamal, ram_kamal, drramkamal, etc.
        return normalized.Contains("ramkamal");
    }
}
