using System.Text;
using Mirage.Graphics.Resources;
using SDL = global::SDL3.SDL;

namespace Mirage.SDL3.Rendering.Resources;

internal sealed class SDL3Shader : IDisposable
{
    private readonly IntPtr _device;

    public unsafe SDL3Shader(IntPtr device, Shader source)
    {
        _device = device;
        Source = source;
        Layout = SDL3SpirV.Inspect(source);
        var entryPoint = Encoding.UTF8.GetBytes(source.EntryPoint + '\0');
        fixed (byte* code = source.Code.Span)
        fixed (byte* entry = entryPoint)
        {
            Native = SDL.CreateGPUShader(
                device,
                new SDL.GPUShaderCreateInfo
                {
                    Code = (IntPtr)code,
                    CodeSize = checked((nuint)source.Code.Length),
                    _entrypoint = (IntPtr)entry,
                    Format = SDL.GPUShaderFormat.SPIRV,
                    Stage =
                        source.Stage == ShaderStage.Vertex
                            ? SDL.GPUShaderStage.Vertex
                            : SDL.GPUShaderStage.Fragment,
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

    public Shader Source { get; }

    public void Dispose()
    {
        if (Native == IntPtr.Zero)
            return;
        SDL.ReleaseGPUShader(_device, Native);
        Native = IntPtr.Zero;
    }
}
