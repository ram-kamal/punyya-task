using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using PHF.Core.Interfaces;

namespace PHF.Infrastructure.Services;

public class LocalStorageService : IStorageService
{
    private readonly string _storageRoot;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public LocalStorageService(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:LocalPath"];
        _storageRoot = !string.IsNullOrWhiteSpace(configuredPath) 
            ? configuredPath 
            : Path.Combine(AppContext.BaseDirectory, "storage", "deliverables");

        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents & Text
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".ods", ".odp",
        ".json", ".xml", ".yaml", ".yml", ".md", ".log",
        // Images (all standard formats)
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".ico", ".tiff", ".tif", ".heic", ".heif", ".avif", ".raw", ".psd", ".ai",
        // Videos (all standard formats)
        ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".flv", ".m4v", ".3gp", ".mpeg", ".mpg",
        // Audio
        ".mp3", ".wav", ".aac", ".m4a", ".ogg", ".flac", ".wma",
        // Archives & Compressed
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"
    };

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".sh",
        ".msi", ".com", ".scr", ".pif", ".application", ".gadget",
        ".hta", ".cpl", ".msc", ".jar", ".jsp", ".asp", ".aspx", ".php"
    };

    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    public async Task<string> SaveFileAsync(string fileName, Stream contentStream, string contentType, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || BlockedExtensions.Contains(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException($"File extension '{ext}' is not permitted for upload.");
        }

        if (contentStream.CanSeek && contentStream.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("File size exceeds maximum permitted limit of 100 MB.");
        }

        var safeFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var fullPath = Path.Combine(_storageRoot, uniqueFileName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[81920];
            long totalBytes = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > MaxFileSizeBytes)
                {
                    fileStream.Dispose();
                    try { File.Delete(fullPath); } catch { }
                    throw new ArgumentException("File size exceeds maximum permitted limit of 100 MB.");
                }
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }

        // Return relative web storage path
        return $"/storage/deliverables/{uniqueFileName}";
    }

    public Task<(Stream Stream, string ContentType, string FileName)?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(storagePath);
        var fullPath = Path.Combine(_storageRoot, fileName);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<(Stream Stream, string ContentType, string FileName)?>(null);
        }

        if (!_contentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<(Stream Stream, string ContentType, string FileName)?>((stream, contentType, fileName));
    }
}
