using OpenGE.Common;

namespace OpenGE.Graphics.Resources;

/// <summary>
/// Specifies the in-memory layout of image pixels.
/// </summary>
public enum RasterImageFormat
{
    /// <summary>
    /// Four 8-bit channels in red, green, blue, alpha order.
    /// </summary>
    Rgba8,
}

/// <summary>
/// Represents decoded two-dimensional pixel data.
/// </summary>
/// <remarks>
/// An image contains pixels in CPU memory. It is independent from the encoded
/// file format and from the SDL GPU texture created for rendering.
/// The image copies and owns its pixel buffer.
/// </remarks>
public class Image : Resource
{
    private ReadOnlyMemory<byte> _data;

    /// <summary>
    /// Gets the in-memory pixel format.
    /// </summary>
    public readonly RasterImageFormat Format;

    /// <summary>
    /// Gets the image height, in pixels.
    /// </summary>
    public readonly uint Height;

    /// <summary>
    /// Gets the image width, in pixels.
    /// </summary>
    public readonly uint Width;

    /// <summary>
    /// Initializes a new instance of the <see cref="Image"/> class.
    /// </summary>
    /// <param name="width">
    /// The image width, in pixels.
    /// </param>
    /// <param name="height">
    /// The image height, in pixels.
    /// </param>
    /// <param name="data">
    /// The pixel data ordered from left to right and top to bottom.
    /// </param>
    /// <param name="format">
    /// The in-memory format of each pixel.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either image dimension is zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the supplied data size does not match the image dimensions
    /// and pixel format.
    /// </exception>
    public Image(
        uint width,
        uint height,
        ReadOnlyMemory<byte> data,
        RasterImageFormat format = RasterImageFormat.Rgba8
    )
    {
        if (width != 0)
        {
            if (height != 0)
            {
                var expectedSize = CalculateDataSize(width, height, format);

                if (data.Length != expectedSize)
                {
                    throw new ArgumentException(
                        $"Image data contains {data.Length} bytes, but "
                            + $"{expectedSize} bytes were expected.",
                        nameof(data)
                    );
                }

                Width = width;
                Height = height;
                Format = format;

                _data = data.ToArray();
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
    }

    /// <summary>
    /// Gets the pixel data.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the image has been destroyed.
    /// </exception>
    public ReadOnlyMemory<byte> Data
    {
        get
        {
            ThrowIfDestroyed();
            return _data;
        }
    }

    /// <summary>
    /// Gets the number of bytes occupied by the image data.
    /// </summary>
    public int DataSize
    {
        get
        {
            ThrowIfDestroyed();
            return _data.Length;
        }
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _data = ReadOnlyMemory<byte>.Empty;

        base.OnDestroy();
    }

    /// <summary>
    /// Calculates the number of bytes required by an image.
    /// </summary>
    /// <param name="width">
    /// The image width, in pixels.
    /// </param>
    /// <param name="height">
    /// The image height, in pixels.
    /// </param>
    /// <param name="format">
    /// The image pixel format.
    /// </param>
    /// <returns>
    /// The required number of bytes.
    /// </returns>
    public static int CalculateDataSize(uint width, uint height, RasterImageFormat format)
    {
        return checked((int)(width * height * GetBytesPerPixel(format)));
    }

    /// <summary>
    /// Gets the number of bytes occupied by one pixel.
    /// </summary>
    /// <param name="format">
    /// The pixel format to inspect.
    /// </param>
    /// <returns>
    /// The number of bytes occupied by one pixel.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the supplied format is unsupported.
    /// </exception>
    public static uint GetBytesPerPixel(RasterImageFormat format)
    {
        return format switch
        {
            RasterImageFormat.Rgba8 => 4,

            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }
}
