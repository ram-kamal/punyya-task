namespace PHF.Core.Interfaces;

public interface IStorageService
{
    Task<string> SaveFileAsync(string fileName, Stream contentStream, string contentType, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType, string FileName)?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default);
}
