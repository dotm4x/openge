using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Resources.Targets;
using OpenGE.Raylib.Windowing;
using OpenGE.Rendering;

namespace OpenGE.Raylib.Rendering;

/// <summary>
/// Executes OpenGE rendering commands using Raylib.
/// </summary>
public sealed class RaylibRenderBackend : RenderBackend
{
    private readonly RaylibWindow _window;

    /// <summary>
    /// Initializes a new instance of the <see cref="RaylibRenderBackend"/> class.
    /// </summary>
    public RaylibRenderBackend(RaylibWindow window)
        : base("Raylib")
    {
        _window = window;
    }

    /// <inheritdoc />
    protected override void OnBeginFrame()
    {
        if (!_window.Opened.Get())
            throw new InvalidOperationException("The Raylib window must be open before rendering.");

        Raylib_cs.Raylib.BeginDrawing();
    }

    /// <inheritdoc />
    protected override void OnEndFrame()
    {
        Raylib_cs.Raylib.EndDrawing();
    }

    /// <inheritdoc />
    protected override void OnBeginRender(RenderTarget? target)
    {
        if (target is not null)
            throw new NotSupportedException("Offscreen rendering is not implemented yet.");
    }

    /// <inheritdoc />
    protected override void OnRender(IRenderCommand command, IRenderContext? context)
    {
        switch (command)
        {
            case ClearCommand clear:
                var color = clear.Color;

                Raylib_cs.Raylib.ClearBackground(
                    new Raylib_cs.Color(color.R, color.G, color.B, color.A)
                );
                break;

            case Render2DCommand:
                throw new NotSupportedException("Render2D commands are not implemented yet.");

            default:
                throw new NotSupportedException(
                    $"Unsupported rendering command: {command.GetType().Name}"
                );
        }
    }
}
