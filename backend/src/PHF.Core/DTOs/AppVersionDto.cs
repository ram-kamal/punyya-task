namespace PHF.Core.DTOs;

public class AppVersionDto
{
    public string LatestVersion { get; set; } = "1.0.3";
    public int VersionCode { get; set; } = 103;
    public string ReleaseNotes { get; set; } = "• Classified IDEs and development environments as active (Antigravity, VS Code, Android Studio, Emulators).\n• Total work shift duration accumulation across multiple daily clock-in/out cycles.\n• Modern Apple Design System & Fluent Dark Glassmorphism.\n• Enhanced screen reader accessibility and UI optimizations.";
    public string DownloadUrl { get; set; } = "https://punyya.org/task/downloads/PunnyaHeartDesk.exe";
    public string FallbackDownloadUrl { get; set; } = "https://punnya-api.onrender.com/downloads/PunnyaHeartDesk.exe";
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;
    public bool IsMandatory { get; set; } = false;
}
