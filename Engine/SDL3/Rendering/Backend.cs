using System.Numerics;
using System.Runtime.InteropServices;
using OpenGE.Common;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Geometry;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Primitives;
using OpenGE.Graphics.Resources;
using OpenGE.Rendering;
using OpenGE.SDL3.Rendering.Resources;
using OpenGE.SDL3.Windowing;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Rendering;

/// <summary>Executes clear and two-dimensional geometry commands using SDL GPU.</summary>
/// <remarks>
/// Uses SPIR-V/Vulkan, triangle lists, straight-alpha blending, RGBA8 textures,
/// and the shader ABI documented with the supplied shaders. GPU resources are cached
/// by source identity. Resources associated with destroyable sources are released when those
/// sources are destroyed; mesh buffers are retained until the backend is destroyed.
/// The window is borrowed; open it before rendering and destroy this backend before closing it.
/// All backend operations must run on the window thread. CPU resources must remain immutable.
/// </remarks>
public sealed class SDL3RenderBackend : RenderBackend
{
    private readonly bool _debugMode;

    private readonly Dictionary<GraphicMesh2D, MeshBuffers> _meshes = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly Dictionary<
        (SDL3Shader Vertex, SDL3Shader Fragment, SDL.GPUTextureFormat TargetFormat),
        SDL3Pipeline
    > _pipelines = [];

    private readonly Dictionary<TextureSampler, SDL3Sampler> _samplers = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly Dictionary<Shader, SDL3Shader> _shaders = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly Dictionary<Texture, SDL3Texture> _textures = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly bool _vsync;
    private readonly SDL3Window _window;
    private IntPtr _claimedWindow;
    private IntPtr _commandBuffer;
    private IntPtr _device;
    private uint _height;
    private Vector2 _logicalSize;
    private IntPtr _renderPass;
    private IntPtr _swapchainTexture;
    private SDL.GPUTextureFormat _targetFormat;
    private bool _targetInitialized;
    private int _threadId;
    private uint _width;

    /// <summary>Creates a backend for one borrowed SDL window.</summary>
    /// <param name="window">The window to render into.</param>
    /// <param name="debugMode">Whether SDL GPU validation is enabled.</param>
    /// <param name="vsync">Whether presentation waits for vertical synchronization.</param>
    public SDL3RenderBackend(SDL3Window window, bool debugMode = false, bool vsync = true)
        : base("SDL3")
    {
        _window = window;
        _debugMode = debugMode;
        _vsync = vsync;
    }

    private void AbandonFrame()
    {
        EndPass();
        if (_commandBuffer == IntPtr.Zero)
            return;
        var commandBuffer = _commandBuffer;
        var acquired = _swapchainTexture != IntPtr.Zero;
        ResetFrame();
        // SDL forbids cancellation after acquiring a swapchain texture.
        var success = acquired
            ? SDL.SubmitGPUCommandBuffer(commandBuffer)
            : SDL.CancelGPUCommandBuffer(commandBuffer);
        if (!success)
        {
            var error = SDL3Gpu.Error("Abandoning the GPU frame");
            ReleaseResources();
            throw error;
        }
        if (!acquired)
            ReleaseResources();
    }

    private void BeginPass(Color? clear = null)
    {
        if (_renderPass != IntPtr.Zero && clear is null)
            return;
        EndPass();
        var shouldClear = clear.HasValue || !_targetInitialized;
        var color = clear.GetValueOrDefault();
        SDL.GPUColorTargetInfo[] targets =
        [
            new()
            {
                Texture = _swapchainTexture,
                LoadOp = shouldClear ? SDL.GPULoadOp.Clear : SDL.GPULoadOp.Load,
                StoreOp = SDL.GPUStoreOp.Store,
                ClearColor = new SDL.FColor
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = color.A,
                },
            },
        ];
        _renderPass = SDL.BeginGPURenderPass(_commandBuffer, targets, 1, IntPtr.Zero);
        if (_renderPass == IntPtr.Zero)
            throw SDL3Gpu.Error("Beginning a GPU render pass");
        _targetInitialized = true;
    }

    private static void CheckAlive(Resource resource)
    {
        if (resource.Destroyed)
            throw new ObjectDisposedException(resource.GetType().Name);
    }

    private void CheckThread()
    {
        if (_threadId != 0 && _threadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException(
                "Use the SDL GPU backend only on its creating thread."
            );
    }

    private void EndPass()
    {
        if (_renderPass == IntPtr.Zero)
            return;
        SDL.EndGPURenderPass(_renderPass);
        _renderPass = IntPtr.Zero;
    }

    private void EnsureDevice()
    {
        CheckThread();
        if (!_window.Opened.Get() || _window.Native == IntPtr.Zero)
            throw new InvalidOperationException("The SDL window must be open before rendering.");
        if (_device != IntPtr.Zero)
        {
            if (_claimedWindow != _window.Native)
                throw new InvalidOperationException(
                    "The window was recreated. Create a new backend for it."
                );
            return;
        }
        _threadId = Environment.CurrentManagedThreadId;
        _device = SDL.CreateGPUDevice(SDL.GPUShaderFormat.SPIRV, _debugMode, null);
        if (_device == IntPtr.Zero)
            throw SDL3Gpu.Error("Creating the SDL GPU device");
        try
        {
            if (!SDL.ClaimWindowForGPUDevice(_device, _window.Native))
                throw SDL3Gpu.Error("Claiming the SDL window");
            _claimedWindow = _window.Native;
            var mode = _vsync ? SDL.GPUPresentMode.VSync : SDL.GPUPresentMode.Immediate;
            if (
                !SDL.SetGPUSwapchainParameters(
                    _device,
                    _claimedWindow,
                    SDL.GPUSwapchainComposition.SDR,
                    mode
                )
            )
                throw SDL3Gpu.Error("Setting GPU presentation parameters");
        }
        catch
        {
            if (_claimedWindow != IntPtr.Zero)
                SDL.ReleaseWindowFromGPUDevice(_device, _claimedWindow);
            SDL.DestroyGPUDevice(_device);
            _device = _claimedWindow = IntPtr.Zero;
            throw;
        }
    }

    private MeshBuffers GetMesh(GraphicMesh2D mesh)
    {
        if (_meshes.TryGetValue(mesh, out var result))
            return result;

        var vertices = mesh.Vertices.Span;
        var packed = new SDL3Vertex2D[vertices.Length];

        for (var index = 0; index < vertices.Length; index++)
            packed[index] = new SDL3Vertex2D(vertices[index]);

        EndPass(); // Copy passes may not overlap a render pass.

        var vertexBuffer = new SDL3Buffer(
            _device,
            _commandBuffer,
            SDL.GPUBufferUsageFlags.Vertex,
            MemoryMarshal.AsBytes(packed.AsSpan())
        );

        SDL3Buffer indexBuffer;
        try
        {
            // Mesh<TVertex> always contains a nonempty sequential or explicit
            // 32-bit index array, so every GraphicMesh2D uses indexed drawing.
            indexBuffer = new SDL3Buffer(
                _device,
                _commandBuffer,
                SDL.GPUBufferUsageFlags.Index,
                MemoryMarshal.AsBytes(mesh.Indices.Span)
            );
        }
        catch
        {
            vertexBuffer.Dispose();
            throw;
        }

        try
        {
            result = new MeshBuffers
            {
                Vertices = vertexBuffer,
                Indices = indexBuffer,
                IndexCount = checked((uint)mesh.Indices.Length),
            };

            _meshes.Add(mesh, result);

            return result;
        }
        catch
        {
            indexBuffer.Dispose();
            vertexBuffer.Dispose();
            throw;
        }
    }

    private SDL3Shader GetShader(Shader shader)
    {
        CheckAlive(shader);
        if (_shaders.TryGetValue(shader, out var result))
            return result;
        result = new SDL3Shader(_device, shader);
        try
        {
            _shaders.Add(shader, result);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private SDL.GPUTextureSamplerBinding GetTextureBinding(TextureBinding binding)
    {
        CheckAlive(binding.Texture);
        CheckAlive(binding.Texture.Image);
        CheckAlive(binding.Sampler);
        if (!_textures.TryGetValue(binding.Texture, out var texture))
        {
            EndPass();
            texture = new SDL3Texture(_device, _commandBuffer, binding.Texture);
            try
            {
                _textures.Add(binding.Texture, texture);
            }
            catch
            {
                texture.Dispose();
                throw;
            }
        }
        if (!_samplers.TryGetValue(binding.Sampler, out var sampler))
        {
            sampler = new SDL3Sampler(_device, binding.Sampler);
            try
            {
                _samplers.Add(binding.Sampler, sampler);
            }
            catch
            {
                sampler.Dispose();
                throw;
            }
        }
        return new SDL.GPUTextureSamplerBinding
        {
            Texture = texture.Native,
            Sampler = sampler.Native,
        };
    }

    private void ReleaseResources()
    {
        foreach (var value in _pipelines.Values)
            value.Dispose();
        foreach (var value in _meshes.Values)
            value.Dispose();
        foreach (var value in _textures.Values)
            value.Dispose();
        foreach (var value in _samplers.Values)
            value.Dispose();
        foreach (var value in _shaders.Values)
            value.Dispose();
        _pipelines.Clear();
        _meshes.Clear();
        _textures.Clear();
        _samplers.Clear();
        _shaders.Clear();
    }

    private void RemoveDestroyedResources()
    {
        RemoveWhere(
            _pipelines,
            key => key.Vertex.Source.Destroyed || key.Fragment.Source.Destroyed
        );
        RemoveWhere(_textures, key => key.Destroyed || key.Image.Destroyed);
        RemoveWhere(_samplers, key => key.Destroyed);
        RemoveWhere(_shaders, key => key.Destroyed);
    }

    private static void RemoveWhere<TKey, TValue>(
        Dictionary<TKey, TValue> cache,
        Func<TKey, bool> predicate
    )
        where TKey : notnull
        where TValue : IDisposable
    {
        foreach (var key in cache.Keys.Where(predicate).ToArray())
        {
            cache[key].Dispose();
            cache.Remove(key);
        }
    }

    private unsafe void Render2D(Render2DCommand command, IRenderContext context)
    {
        CheckAlive(command.Material);
        if (command.Mesh.Vertices.IsEmpty)
            return;
        if (context.Viewport.Size.X <= 0 || context.Viewport.Size.Y <= 0)
            return;
        var vertex = GetShader(command.Material.VertexShader);
        var fragment = GetShader(command.Material.FragmentShader);
        var bindingCount = Math.Max(vertex.Layout.Samplers, fragment.Layout.Samplers);
        if (command.Material.Textures.Count < bindingCount)
            throw new ArgumentException(
                $"The shaders require {bindingCount} texture bindings, but the material supplies {command.Material.Textures.Count}."
            );

        var key = (Vertex: vertex, Fragment: fragment, TargetFormat: _targetFormat);
        if (!_pipelines.TryGetValue(key, out var pipeline))
        {
            pipeline = new SDL3Pipeline(_device, vertex, fragment, _targetFormat);
            try
            {
                _pipelines.Add(key, pipeline);
            }
            catch
            {
                pipeline.Dispose();
                throw;
            }
        }
        var mesh = GetMesh(command.Mesh);
        // A material slot has the same index in both stages. Unused trailing slots are ignored.
        var bindings = new SDL.GPUTextureSamplerBinding[bindingCount];
        for (var i = 0; i < bindings.Length; i++)
            bindings[i] = GetTextureBinding(command.Material.Textures[i]);

        BeginPass();
        if (!SetView(context))
            return;
        SDL.BindGPUGraphicsPipeline(_renderPass, pipeline.Native);
        SDL.GPUBufferBinding[] vertexBindings = [new() { Buffer = mesh.Vertices.Native }];
        SDL.BindGPUVertexBuffers(_renderPass, 0, vertexBindings, 1);
        if (vertex.Layout.Samplers > 0)
            SDL.BindGPUVertexSamplers(_renderPass, 0, bindings, vertex.Layout.Samplers);
        if (fragment.Layout.Samplers > 0)
            SDL.BindGPUFragmentSamplers(_renderPass, 0, bindings, fragment.Layout.Samplers);

        if (vertex.Layout.UniformBuffers != 0)
        {
            // System.Numerics uses row vectors. The shader explicitly consumes these four rows.
            var mvp =
                new Matrix4x4(command.Transform)
                * context.Camera.View
                * context.Camera.GetProjection(context.Viewport);
            SDL.PushGPUVertexUniformData(_commandBuffer, 0, (IntPtr)(&mvp), 64);
        }
        SDL.BindGPUIndexBuffer(
            _renderPass,
            new SDL.GPUBufferBinding { Buffer = mesh.Indices.Native },
            SDL.GPUIndexElementSize.IndexElementSize32Bit
        );
        SDL.DrawGPUIndexedPrimitives(_renderPass, mesh.IndexCount, 1, 0, 0, 0);
    }

    private void ResetFrame()
    {
        _commandBuffer = _swapchainTexture = _renderPass = IntPtr.Zero;
        _targetInitialized = false;
        _width = _height = 0;
    }

    private bool SetView(IRenderContext context)
    {
        var viewport = context.Viewport;
        var position = viewport.Position;
        var size = viewport.Size;
        if (
            !float.IsFinite(position.X)
            || !float.IsFinite(position.Y)
            || !float.IsFinite(size.X)
            || !float.IsFinite(size.Y)
        )
            throw new ArgumentException("Viewport coordinates must be finite.", nameof(context));
        if (size.X <= 0 || size.Y <= 0 || _logicalSize.X <= 0 || _logicalSize.Y <= 0)
            return false;
        if (
            position.X < 0
            || position.Y < 0
            || position.X + size.X > _logicalSize.X
            || position.Y + size.Y > _logicalSize.Y
        )
            throw new ArgumentException(
                "The viewport must fit inside the logical window bounds.",
                nameof(context)
            );
        var scale = new Vector2(_width / _logicalSize.X, _height / _logicalSize.Y);
        var start = position * scale;
        var extent = size * scale;
        SDL.SetGPUViewport(
            _renderPass,
            new SDL.GPUViewport
            {
                X = start.X,
                Y = start.Y,
                W = extent.X,
                H = extent.Y,
                MinDepth = 0,
                MaxDepth = 1,
            }
        );
        var left = Math.Clamp((int)MathF.Floor(start.X), 0, (int)_width);
        var top = Math.Clamp((int)MathF.Floor(start.Y), 0, (int)_height);
        var right = Math.Clamp((int)MathF.Ceiling(start.X + extent.X), left, (int)_width);
        var bottom = Math.Clamp((int)MathF.Ceiling(start.Y + extent.Y), top, (int)_height);
        SDL.SetGPUScissor(
            _renderPass,
            new SDL.Rect
            {
                X = left,
                Y = top,
                W = right - left,
                H = bottom - top,
            }
        );
        return right > left && bottom > top;
    }

    /// <inheritdoc />
    protected override void OnBeginFrame()
    {
        EnsureDevice();
        RemoveDestroyedResources();
        _logicalSize = _window.Size.Get();
        _commandBuffer = SDL.AcquireGPUCommandBuffer(_device);
        if (_commandBuffer == IntPtr.Zero)
            throw SDL3Gpu.Error("Acquiring a GPU command buffer");
        try
        {
            if (
                !SDL.WaitAndAcquireGPUSwapchainTexture(
                    _commandBuffer,
                    _claimedWindow,
                    out _swapchainTexture,
                    out _width,
                    out _height
                )
            )
                throw SDL3Gpu.Error("Acquiring the swapchain texture");
            if (_swapchainTexture == IntPtr.Zero)
            {
                var commandBuffer = _commandBuffer;
                ResetFrame();
                if (!SDL.CancelGPUCommandBuffer(commandBuffer))
                    throw SDL3Gpu.Error("Cancelling an invisible frame");
                return;
            }
            _targetFormat = SDL.GetGPUSwapchainTextureFormat(_device, _claimedWindow);
        }
        catch (Exception exception)
        {
            try
            {
                AbandonFrame();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "GPU frame operation and cleanup failed.",
                    exception,
                    cleanupFailure
                );
            }
            throw;
        }
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        CheckThread();
        AbandonFrame();
        if (_device != IntPtr.Zero)
        {
            if (!SDL.WaitForGPUIdle(_device))
                throw SDL3Gpu.Error("Waiting for the GPU before destruction");
            ReleaseResources();
            if (_claimedWindow != IntPtr.Zero && _window.Native == _claimedWindow)
                SDL.ReleaseWindowFromGPUDevice(_device, _claimedWindow);
            SDL.DestroyGPUDevice(_device);
            _device = _claimedWindow = IntPtr.Zero;
        }
        base.OnDestroy();
    }

    /// <inheritdoc />
    protected override void OnEndFrame()
    {
        CheckThread();
        if (_commandBuffer == IntPtr.Zero)
            return;
        try
        {
            if (_swapchainTexture != IntPtr.Zero && !_targetInitialized)
                BeginPass();
            EndPass();
        }
        catch (Exception exception)
        {
            try
            {
                AbandonFrame();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "GPU frame operation and cleanup failed.",
                    exception,
                    cleanupFailure
                );
            }
            throw;
        }
        var commandBuffer = _commandBuffer;
        var acquired = _swapchainTexture != IntPtr.Zero;
        ResetFrame(); // SDL invalidates the command buffer even if submission reports failure.
        var success = acquired
            ? SDL.SubmitGPUCommandBuffer(commandBuffer)
            : SDL.CancelGPUCommandBuffer(commandBuffer);
        if (!success)
        {
            var error = SDL3Gpu.Error("Finishing the GPU frame");
            ReleaseResources(); // Do not retain cached uploads from a failed submission.
            throw error;
        }
    }

    /// <inheritdoc />
    protected override void OnRender(IRenderCommand command, IRenderContext? context)
    {
        CheckThread();
        if (_swapchainTexture == IntPtr.Zero)
            return; // A minimized/occluded window may have no drawable texture.
        switch (command)
        {
            case ClearCommand clear:
                BeginPass(clear.Color);
                break;
            case Render2DCommand render:
                Render2D(
                    render,
                    context
                        ?? throw new ArgumentNullException(
                            nameof(context),
                            "Render2D requires a camera and viewport."
                        )
                );
                break;
            default:
                throw new NotSupportedException(
                    $"Render command '{command.GetType().Name}' is not supported."
                );
        }
    }

    private sealed class MeshBuffers : IDisposable
    {
        public required uint IndexCount { get; init; }
        public required SDL3Buffer Indices { get; init; }
        public required SDL3Buffer Vertices { get; init; }

        public void Dispose()
        {
            Indices.Dispose();
            Vertices.Dispose();
        }
    }
}
