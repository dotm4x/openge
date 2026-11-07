using System.Text;
using OpenGE.Graphics.Resources;
using SDL3;

namespace OpenGE.SDL3.Rendering.Resources;

internal sealed class SDL3Shader : IDisposable
{
    private readonly IntPtr _device;

    /// <summary>
    /// Initializes a new instance of the <see cref="SDL3Shader"/> class.
    /// </summary>
    public unsafe SDL3Shader(IntPtr device, ShaderBinary source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (device == IntPtr.Zero)
            throw new ArgumentException("The GPU device cannot be null.", nameof(device));

        if (source.Destroyed)
            throw new ObjectDisposedException(nameof(source));

        if (source.Format != ShaderFormat.SpirV)
            throw new NotSupportedException(
                $"Shader format '{source.Format}' is not supported. "
                    + "This backend requires SPIR-V."
            );

        var stage = source.Stage switch
        {
            ShaderStage.Vertex => SDL.GPUShaderStage.Vertex,
            ShaderStage.Fragment => SDL.GPUShaderStage.Fragment,
            _ => throw new NotSupportedException(
                $"Shader stage '{source.Stage}' is not supported " + "by this graphics pipeline."
            ),
        };

        if (source.EntryPoint.Contains('\0'))
            throw new ArgumentException(
                "The shader entry point cannot contain null characters.",
                nameof(source)
            );

        _device = device;
        Source = source;
        Layout = SDL3SpirV.Inspect(source);

        var bytecode = source.Code;
        var entryPoint = Encoding.UTF8.GetBytes(source.EntryPoint + '\0');

        fixed (byte* code = bytecode.Span)
        fixed (byte* entry = entryPoint)
        {
            Native = SDL.CreateGPUShader(
                device,
                new SDL.GPUShaderCreateInfo
                {
                    Code = (IntPtr)code,
                    CodeSize = checked((nuint)bytecode.Length),
                    _entrypoint = (IntPtr)entry,
                    Format = SDL.GPUShaderFormat.SPIRV,
                    Stage = stage,
                    NumSamplers = Layout.Samplers,
                    NumUniformBuffers = Layout.UniformBuffers,
                    NumStorageBuffers = 0,
                    NumStorageTextures = 0,
                }
            );
        }

        if (Native == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating a GPU shader");
    }

    public SDL3ShaderLayout Layout { get; }

    public IntPtr Native { get; private set; }

    public ShaderBinary Source { get; }

    public void Dispose()
    {
        if (Native == IntPtr.Zero)
            return;

        SDL.ReleaseGPUShader(_device, Native);
        Native = IntPtr.Zero;
    }
}
