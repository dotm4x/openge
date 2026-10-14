using OpenGE.Graphics.Resources;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Rendering.Resources;

internal sealed class SDL3Texture : IDisposable
{
    private readonly IntPtr _device;

    public SDL3Texture(IntPtr device, IntPtr commandBuffer, Texture source)
    {
        _device = device;
        var image = source.Image;
        if (image.Format != RasterImageFormat.Rgba8)
            throw new NotSupportedException($"Image format '{image.Format}' is not supported.");

        var pixels = image.Data.Span;
        var rowBytes = checked((int)image.Width * 4);
        // Align rows to 256 bytes; each fresh transfer buffer starts at offset zero.
        var rowPitch = checked((rowBytes + 255) & ~255);
        var packed = new byte[checked(rowPitch * (int)image.Height)];
        for (var y = 0; y < image.Height; y++)
            pixels.Slice(checked(y * rowBytes), rowBytes).CopyTo(packed.AsSpan(y * rowPitch));

        Native = SDL.CreateGPUTexture(
            device,
            new SDL.GPUTextureCreateInfo
            {
                Type = SDL.GPUTextureType.TextureType2D,
                Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
                Usage = SDL.GPUTextureUsageFlags.Sampler,
                Width = image.Width,
                Height = image.Height,
                LayerCountOrDepth = 1,
                NumLevels = 1,
                SampleCount = SDL.GPUSampleCount.SampleCount1,
            }
        );
        if (Native == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating a GPU texture");

        try
        {
            using var transfer = new SDL3TransferBuffer(device, packed);
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

    public IntPtr Native { get; private set; }

    public void Dispose()
    {
        if (Native == IntPtr.Zero)
            return;
        SDL.ReleaseGPUTexture(_device, Native);
        Native = IntPtr.Zero;
    }
}
