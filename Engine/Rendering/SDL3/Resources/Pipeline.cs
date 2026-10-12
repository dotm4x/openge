using System.Numerics;
using System.Runtime.InteropServices;
using Mirage.Graphics.Geometry;
using SDL = global::SDL3.SDL;

namespace Mirage.Rendering.SDL3.Resources;

internal sealed class SDL3Pipeline : IDisposable
{
    private readonly IntPtr _device;

    public unsafe SDL3Pipeline(
        IntPtr device,
        SDL3Shader vertex,
        SDL3Shader fragment,
        SDL.GPUTextureFormat targetFormat
    )
    {
        _device = device;
        var buffer = new SDL.GPUVertexBufferDescription
        {
            Slot = 0,
            Pitch = 32,
            InputRate = SDL.GPUVertexInputRate.Vertex,
        };
        SDL.GPUVertexAttribute* attributes = stackalloc SDL.GPUVertexAttribute[3];
        attributes[0] = new()
        {
            Location = 0,
            BufferSlot = 0,
            Format = SDL.GPUVertexElementFormat.Float2,
            Offset = 0,
        };
        attributes[1] = new()
        {
            Location = 1,
            BufferSlot = 0,
            Format = SDL.GPUVertexElementFormat.Float2,
            Offset = 8,
        };
        attributes[2] = new()
        {
            Location = 2,
            BufferSlot = 0,
            Format = SDL.GPUVertexElementFormat.Float4,
            Offset = 16,
        };

        var target = new SDL.GPUColorTargetDescription
        {
            Format = targetFormat,
            BlendState = new SDL.GPUColorTargetBlendState
            {
                EnableBlend = true,
                SrcColorBlendFactor = SDL.GPUBlendFactor.SrcAlpha,
                DstColorBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = SDL.GPUBlendOp.Add,
                SrcAlphaBlendFactor = SDL.GPUBlendFactor.One,
                DstAlphaBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = SDL.GPUBlendOp.Add,
                EnableColorWriteMask = false,
            },
        };
        Native = SDL.CreateGPUGraphicsPipeline(
            device,
            new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader = vertex.Native,
                FragmentShader = fragment.Native,
                VertexInputState = new SDL.GPUVertexInputState
                {
                    VertexBufferDescriptions = (IntPtr)(&buffer),
                    NumVertexBuffers = 1,
                    VertexAttributes = (IntPtr)attributes,
                    NumVertexAttributes = 3,
                },
                PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
                RasterizerState = new SDL.GPURasterizerState
                {
                    FillMode = SDL.GPUFillMode.Fill,
                    CullMode = SDL.GPUCullMode.None,
                    FrontFace = SDL.GPUFrontFace.CounterClockwise,
                    EnableDepthClip = true,
                },
                MultisampleState = new SDL.GPUMultisampleState
                {
                    SampleCount = SDL.GPUSampleCount.SampleCount1,
                },
                TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
                {
                    ColorTargetDescriptions = (IntPtr)(&target),
                    NumColorTargets = 1,
                    HasDepthStencilTarget = false,
                },
            }
        );
        if (Native == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating a 2D graphics pipeline");
    }

    public IntPtr Native { get; private set; }

    public void Dispose()
    {
        if (Native == IntPtr.Zero)
            return;
        SDL.ReleaseGPUGraphicsPipeline(_device, Native);
        Native = IntPtr.Zero;
    }
}

/// <summary>Defines the native vertex ABI independently of the managed Color layout.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal readonly struct SDL3Vertex2D(GraphicVertex2D vertex)
{
    public readonly Vector2 Position = vertex.Position;
    public readonly Vector2 TextureCoordinate = vertex.TextureCoordinate;
    public readonly Vector4 Color = new(
        vertex.Color.R,
        vertex.Color.G,
        vertex.Color.B,
        vertex.Color.A
    );
}
