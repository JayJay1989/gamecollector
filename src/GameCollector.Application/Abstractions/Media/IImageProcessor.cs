namespace GameCollector.Application.Abstractions.Media;

public sealed record ValidatedImage(string ContentType, int Width, int Height, string Checksum);

public interface IImageProcessor
{
    ValidatedImage Validate(ReadOnlyMemory<byte> content);
    byte[] CreateThumbnail(ReadOnlyMemory<byte> content);
}
