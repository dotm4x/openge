using System.Numerics;
using OpenGE.Common;
using OpenGE.Graphics;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Primitives;
using OpenGE.Rendering;
using OpenGE.Scheduling;
using OpenGE.Windowing;

namespace Tests.Rendering;

internal sealed record MarkerCommand(string Name) : IRenderCommand;

internal sealed class TestCamera : ICamera
{
    public Matrix4x4 View => Matrix4x4.Identity;

    public List<Viewport> ProjectionViewports { get; } = [];

    public Matrix4x4 GetProjection(Viewport viewport)
    {
        ProjectionViewports.Add(viewport);
        return Matrix4x4.Identity;
    }
}

internal sealed class RecordingRenderable(
    IEnumerable<IRenderCommand> commands,
    Action<IRenderContext>? onRender = null
) : IRenderable
{
    public List<IRenderContext> Contexts { get; } = [];

    public RenderData Render(IRenderContext context)
    {
        Contexts.Add(context);
        onRender?.Invoke(context);
        return new RenderData(commands);
    }
}

internal sealed class TestWindow(string identifier = "test-window")
    : Window(
        new WindowOptions
        {
            Identifier = identifier,
            Title = identifier,
            Size = new Vector2(800, 600),
        }
    )
{
    protected override void OnClose() { }

    protected override void OnModeChanged(WindowMode mode) { }

    protected override void OnMove(Vector2 position) { }

    protected override void OnOpen() { }

    protected override void OnProcess() { }

    protected override void OnResizableChanged(bool resizable) { }

    protected override void OnResize(Vector2 size) { }

    protected override void OnTitleChanged(string title) { }

    protected override void OnVisibilityChanged(bool visible) { }
}

internal sealed record BackendRenderCall(
    OpenGE.Graphics.Resources.Targets.RenderTarget? Target,
    IRenderCommand Command,
    IRenderContext? Context
);

internal sealed class RecordingBackend(string identifier = "recording") : RenderBackend(identifier)
{
    public List<string> Operations { get; } = [];

    public List<BackendRenderCall> RenderCalls { get; } = [];

    public Exception? EndFrameException { get; set; }

    public Action<IRenderCommand, IRenderContext?>? OnCommand { get; set; }

    protected override void OnBeginFrame()
    {
        Operations.Add("begin-frame");
    }

    protected override void OnEndFrame()
    {
        Operations.Add("end-frame");
        if (EndFrameException is not null)
            throw EndFrameException;
    }

    protected override void OnBeginRender(OpenGE.Graphics.Resources.Targets.RenderTarget? target)
    {
        _currentTarget = target;
        Operations.Add(target is null ? "begin-window" : "begin-target");
    }

    protected override void OnRender(IRenderCommand command, IRenderContext? context)
    {
        Operations.Add(
            command is MarkerCommand marker ? $"command:{marker.Name}" : "command:other"
        );
        RenderCalls.Add(new BackendRenderCall(_currentTarget, command, context));
        OnCommand?.Invoke(command, context);
    }

    private OpenGE.Graphics.Resources.Targets.RenderTarget? _currentTarget;
}
