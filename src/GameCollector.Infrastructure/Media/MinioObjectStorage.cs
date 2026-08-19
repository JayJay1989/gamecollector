using GameCollector.Application.Abstractions.Media;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace GameCollector.Infrastructure.Media;

public sealed class MinioObjectStorage(IMinioClient client, IOptions<MediaStorageOptions> options) : IObjectStorage
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task<Uri> CreateUploadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = await client.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(_bucket).WithObject(objectKey).WithExpiry(Seconds(lifetime)));
        return new Uri(url);
    }

    public async Task<Uri> CreateDownloadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = await client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_bucket).WithObject(objectKey).WithExpiry(Seconds(lifetime)));
        return new Uri(url);
    }

    public async Task<byte[]> ReadAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var stat = await client.StatObjectAsync(new StatObjectArgs().WithBucket(_bucket).WithObject(objectKey), cancellationToken);
            if (stat.Size is < 1 || stat.Size > maximumBytes) throw new InvalidDataException("The stored object has an invalid size.");
            using var output = new MemoryStream((int)stat.Size);
            await client.GetObjectAsync(new GetObjectArgs().WithBucket(_bucket).WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(output)), cancellationToken);
            if (output.Length > maximumBytes) throw new InvalidDataException("The stored object is too large.");
            if (output.Length != stat.Size) throw new InvalidDataException("The stored object could not be read completely.");
            return output.ToArray();
        }
        catch (GameCollector.Application.Abstractions.Media.ObjectNotFoundException) { throw; }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            throw new GameCollector.Application.Abstractions.Media.ObjectNotFoundException(objectKey);
        }
        catch (MinioException exception) when (exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                               exception.Message.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            throw new GameCollector.Application.Abstractions.Media.ObjectNotFoundException(objectKey);
        }
    }

    public async Task WriteAsync(string objectKey, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(_bucket).WithObject(objectKey)
            .WithStreamData(stream).WithObjectSize(content.Length).WithContentType(contentType), cancellationToken);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
        await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(objectKey), cancellationToken);

    private static int Seconds(TimeSpan lifetime) => checked((int)lifetime.TotalSeconds);
}
