namespace GameCollector.Application.Abstractions.Media;

public interface IObjectStorage
{
    Task<Uri> CreateUploadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken = default);
    Task<Uri> CreateDownloadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken = default);
    Task WriteAsync(string objectKey, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}

public sealed class ObjectNotFoundException(string objectKey) : Exception($"Object '{objectKey}' was not found.");
