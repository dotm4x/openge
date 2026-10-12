using Mirage.Graphics.Resources;
using SDL = global::SDL3.SDL;

namespace Mirage.SDL3.Rendering.Resources;

internal sealed class SDL3Sampler : IDisposable
{
    private readonly IntPtr _device;

    public SDL3Sampler(IntPtr device, TextureSampler source)
    {
        _device = device;
        Native = SDL.CreateGPUSampler(
            device,
            new SDL.GPUSamplerCreateInfo
            {
                MinFilter = Filter(source.MinFilter),
                MagFilter = Filter(source.MagFilter),
                AddressModeU = Address(source.AddressU),
                AddressModeV = Address(source.AddressV),
                AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
                MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
                MinLod = 0,
                MaxLod = 0,
                MaxAnisotropy = 1,
                EnableAnisotropy = false,
                EnableCompare = false,
            }
        );
        if (Native == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating a GPU sampler");
    }

    public IntPtr Native { get; private set; }

    public void Dispose()
    {
        if (Native == IntPtr.Zero)
            return;
        SDL.ReleaseGPUSampler(_device, Native);
        Native = IntPtr.Zero;
    }

    private static SDL.GPUSamplerAddressMode Address(TextureAddressMode value) =>
        value switch
        {
            TextureAddressMode.Repeat => SDL.GPUSamplerAddressMode.Repeat,
            TextureAddressMode.MirroredRepeat => SDL.GPUSamplerAddressMode.MirroredRepeat,
            TextureAddressMode.ClampToEdge => SDL.GPUSamplerAddressMode.ClampToEdge,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static SDL.GPUFilter Filter(TextureFilter value) =>
        value switch
        {
            TextureFilter.Nearest => SDL.GPUFilter.Nearest,
            TextureFilter.Linear => SDL.GPUFilter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
}
