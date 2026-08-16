using System.Security.Cryptography;
using GameCollector.Application.Abstractions.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace GameCollector.Infrastructure.Media;

public sealed class ImageSharpImageProcessor : IImageProcessor
{
    private const int MaximumDimension = 8000;
    private const long MaximumPixels = 40_000_000;

    public ValidatedImage Validate(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var stream = new MemoryStream(content.ToArray(), writable: false);
            var format = Image.DetectFormat(stream);
            stream.Position = 0;
            var info = Image.Identify(stream) ?? throw new InvalidDataException("The image header is invalid.");
            if (info.Width < 1 || info.Height < 1 || info.Width > MaximumDimension || info.Height > MaximumDimension ||
                (long)info.Width * info.Height > MaximumPixels || !TryMimeType(format, out var contentType))
                throw new InvalidDataException("The image dimensions or format are invalid.");
            stream.Position = 0;
            using var decoded = Image.Load(stream);
            if (decoded.Frames.Count != 1) throw new InvalidDataException("Animated images are not accepted.");
            return new ValidatedImage(contentType, info.Width, info.Height,
                Convert.ToHexStringLower(SHA256.HashData(content.Span)));
        }
        catch (UnknownImageFormatException exception) { throw new InvalidDataException("The image format is not accepted.", exception); }
        catch (InvalidImageContentException exception) { throw new InvalidDataException("The image content is invalid.", exception); }
    }

    public byte[] CreateThumbnail(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var input = new MemoryStream(content.ToArray(), writable: false);
            using var image = Image.Load(input);
            image.Mutate(context => context.AutoOrient().Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(480, 480)
            }));
            using var output = new MemoryStream();
            image.SaveAsJpeg(output, new JpegEncoder { Quality = 82 });
            return output.ToArray();
        }
        catch (UnknownImageFormatException exception) { throw new InvalidDataException("The image format is not accepted.", exception); }
        catch (InvalidImageContentException exception) { throw new InvalidDataException("The image content is invalid.", exception); }
    }

    private static bool TryMimeType(IImageFormat format, out string contentType)
    {
        contentType = format.DefaultMimeType.ToLowerInvariant();
        return contentType is "image/jpeg" or "image/png" or "image/webp";
    }
}
