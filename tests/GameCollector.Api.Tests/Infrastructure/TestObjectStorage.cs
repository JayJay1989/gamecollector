using System.Collections.Concurrent;
using GameCollector.Application.Abstractions.Media;

namespace GameCollector.Api.Tests.Infrastructure;

public sealed class TestObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
    private int _writesToFail;
    public int WritesToFail { get => Volatile.Read(ref _writesToFail); set => Volatile.Write(ref _writesToFail, value); }

    public Task<Uri> CreateUploadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"https://test-storage.local/upload/{Uri.EscapeDataString(objectKey)}"));

    public Task<Uri> CreateDownloadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"https://test-storage.local/download/{Uri.EscapeDataString(objectKey)}"));

    public Task<byte[]> ReadAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(objectKey, out var content)) throw new ObjectNotFoundException(objectKey);
        if (content.LongLength > maximumBytes) throw new InvalidDataException("Object is too large.");
        return Task.FromResult(content.ToArray());
    }

    public Task WriteAsync(string objectKey, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref _writesToFail) >= 0) throw new IOException("Transient test storage failure.");
        _objects[objectKey] = content.ToArray();
        return Task.CompletedTask;
    }

    public void Upload(Uri uploadUrl, byte[] content)
    {
        var prefix = "/upload/";
        var escapedKey = uploadUrl.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)
            ? uploadUrl.AbsolutePath[prefix.Length..]
            : throw new ArgumentException("This is not a test upload URL.", nameof(uploadUrl));
        _objects[Uri.UnescapeDataString(escapedKey)] = content.ToArray();
    }
}
