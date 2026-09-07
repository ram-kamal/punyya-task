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

    public async Task<string> SaveFileAsync(string fileName, Stream contentStream, string contentType, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var fullPath = Path.Combine(_storageRoot, uniqueFileName);

        using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream, cancellationToken);

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
