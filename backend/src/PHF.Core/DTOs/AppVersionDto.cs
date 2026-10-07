namespace PHF.Core.DTOs;

public class AppVersionDto
{
    public string LatestVersion { get; set; } = "1.0.1";
    public int VersionCode { get; set; } = 101;
    public string ReleaseNotes { get; set; } = "• Auto clock-out on PC shutdown, restart, and window close.\n• Auto clock-out when PC sleeps or shift exceeds 12 hours.\n• 100% accurate attendance & timesheet reporting.\n• One-click in-app update & restart support.";
    public string DownloadUrl { get; set; } = "https://punnya.org/task/downloads/PunnyaHeartDesk.exe";
    public string FallbackDownloadUrl { get; set; } = "https://punnya-api.onrender.com/downloads/PunnyaHeartDesk.exe";
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;
    public bool IsMandatory { get; set; } = false;
}
