using OpenGE.Graphics.Resources;
using OpenGE.Graphics.Resources.Targets;
using OpenGE.Graphics.Resources.Textures;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Rendering.Resources;

/// <summary>
/// Manages a native SDL GPU texture associated with an OpenGE texture.
/// </summary>
/// <remarks>
/// Image textures upload their pixel data to the GPU, while rendering targets
/// allocate empty GPU textures that support both rendering and sampling.
/// </remarks>
internal sealed class SDL3Texture : IDisposable
{
    private readonly IntPtr _device;

    /// <summary>
    /// Initializes a new instance of the <see cref="SDL3Texture"/> class.
    /// </summary>
    /// <param name="device">The SDL GPU device.</param>
    /// <param name="commandBuffer">
    /// The command buffer used to upload image data.
    /// </param>
    /// <param name="source">The source texture.</param>
    public SDL3Texture(IntPtr device, IntPtr commandBuffer, Texture source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Destroyed)
            throw new ObjectDisposedException(nameof(source));

        _device = device;

        switch (source)
        {
            case ImageTexture imageTexture:
                CreateImageTexture(commandBuffer, imageTexture);
                break;

            case RenderTarget renderTarget:
                CreateRenderTarget(renderTarget);
                break;

            default:
                throw new NotSupportedException(
                    $"Texture type '{source.GetType().Name}' is not supported."
                );
        }
    }

    /// <summary>
    /// Gets the native SDL GPU texture handle.
    /// </summary>
    public IntPtr Native { get; private set; }

    /// <summary>
    /// Creates a GPU texture containing the source image pixels.
    /// </summary>
    private void CreateImageTexture(IntPtr commandBuffer, ImageTexture source)
    {
        var image = source.Image;

        if (image.Destroyed)
            throw new ObjectDisposedException(nameof(image));

        if (image.Format != RasterImageFormat.Rgba8)
        {
            throw new NotSupportedException($"Image format '{image.Format}' is not supported.");
        }

        var pixels = image.Data.Span;
        var rowBytes = checked((int)image.Width * 4);

        // Align rows to 256 bytes for GPU texture uploads.
        var rowPitch = checked((rowBytes + 255) & ~255);
        var packed = new byte[checked(rowPitch * (int)image.Height)];

        for (var y = 0; y < image.Height; y++)
        {
            pixels
                .Slice(checked((int)y * rowBytes), rowBytes)
                .CopyTo(packed.AsSpan(checked((int)y * rowPitch)));
        }

        Native = CreateTexture(image.Width, image.Height, SDL.GPUTextureUsageFlags.Sampler);

        try
        {
            using var transfer = new SDL3TransferBuffer(_device, packed);

            var pass = SDL.BeginGPUCopyPass(commandBuffer);

            if (pass == IntPtr.Zero)
                throw SDL3Gpu.Error("Beginning a texture upload");

            try
            {
                SDL.UploadToGPUTexture(
                    pass,
                    new SDL.GPUTextureTransferInfo
                    {
                        TransferBuffer = transfer.Native,
                        PixelsPerRow = checked((uint)rowPitch / 4),
                        RowsPerLayer = image.Height,
                    },
                    new SDL.GPUTextureRegion
                    {
                        Texture = Native,
                        W = image.Width,
                        H = image.Height,
                        D = 1,
                    },
                    false
                );
            }
            finally
            {
                SDL.EndGPUCopyPass(pass);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates an empty GPU texture that can be rendered into and sampled.
    /// </summary>
    private void CreateRenderTarget(RenderTarget source)
    {
        Native = CreateTexture(
            checked((uint)source.Size.X),
            checked((uint)source.Size.Y),
            SDL.GPUTextureUsageFlags.ColorTarget | SDL.GPUTextureUsageFlags.Sampler
        );
    }

    /// <summary>
    /// Allocates a native two-dimensional RGBA8 GPU texture.
    /// </summary>
    private IntPtr CreateTexture(uint width, uint height, SDL.GPUTextureUsageFlags usage)
    {
        var texture = SDL.CreateGPUTexture(
            _device,
            new SDL.GPUTextureCreateInfo
            {
                Type = SDL.GPUTextureType.TextureType2D,
                Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
                Usage = usage,
                Width = width,
                Height = height,
                LayerCountOrDepth = 1,
                NumLevels = 1,
                SampleCount = SDL.GPUSampleCount.SampleCount1,
            }
        );

        if (texture == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating a GPU texture");

        return texture;
    }

    /// <summary>
    /// Releases the native GPU texture.
    /// </summary>
    public void Dispose()
    {
        if (Native == IntPtr.Zero)
            return;

        SDL.ReleaseGPUTexture(_device, Native);
        Native = IntPtr.Zero;
    }
}
