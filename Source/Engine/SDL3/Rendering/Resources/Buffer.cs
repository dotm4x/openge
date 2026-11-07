using System.Runtime.InteropServices;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Rendering.Resources;

internal sealed class SDL3Buffer : IDisposable
{
    private readonly IntPtr _device;

    /// <summary>
    /// Initializes a new instance of the <see cref="SDL3Buffer"/> class.
    /// </summary>
    public SDL3Buffer(
        IntPtr device,
        IntPtr commandBuffer,
        SDL.GPUBufferUsageFlags usage,
        ReadOnlySpan<byte> data
    )
    {
        if (data.IsEmpty)
            throw new ArgumentException("A GPU buffer cannot be empty.", nameof(data));

        _device = device;
        Size = checked((uint)data.Length);
        Native = SDL.CreateGPUBuffer(
            device,
            new SDL.GPUBufferCreateInfo { Usage = usage, Size = Size }
        );
        if (Native == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating a GPU buffer");

        try
        {
            using var transfer = new SDL3TransferBuffer(device, data);
            var pass = SDL.BeginGPUCopyPass(commandBuffer);
            if (pass == IntPtr.Zero)
                throw SDL3Gpu.Error("Beginning a buffer upload");
            try
            {
                SDL.UploadToGPUBuffer(
                    pass,
                    new SDL.GPUTransferBufferLocation { TransferBuffer = transfer.Native },
                    new SDL.GPUBufferRegion { Buffer = Native, Size = Size },
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

    public uint Size { get; }

    public void Dispose()
    {
        if (Native == IntPtr.Zero)
            return;
        SDL.ReleaseGPUBuffer(_device, Native);
        Native = IntPtr.Zero;
    }
}

internal sealed class SDL3TransferBuffer : IDisposable
{
    private readonly IntPtr _device;

    /// <summary>
    /// Initializes a new instance of the <see cref="SDL3TransferBuffer"/> class.
    /// </summary>
    public unsafe SDL3TransferBuffer(IntPtr device, ReadOnlySpan<byte> data)
    {
        _device = device;
        Native = SDL.CreateGPUTransferBuffer(
            device,
            new SDL.GPUTransferBufferCreateInfo
            {
                Usage = SDL.GPUTransferBufferUsage.Upload,
                Size = checked((uint)data.Length),
            }
        );
        if (Native == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating a transfer buffer");

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(device, Native, false);
            if (mapped == IntPtr.Zero)
                throw SDL3Gpu.Error("Mapping a transfer buffer");
            try
            {
                data.CopyTo(new Span<byte>((void*)mapped, data.Length));
            }
            finally
            {
                SDL.UnmapGPUTransferBuffer(device, Native);
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
        SDL.ReleaseGPUTransferBuffer(_device, Native);
        Native = IntPtr.Zero;
    }
}

internal static class SDL3Gpu
{
    public static InvalidOperationException Error(string operation) =>
        new($"{operation} failed: {SDL.GetError()}");
}
