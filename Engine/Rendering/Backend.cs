using OpenGE.Common.Interfaces;
using OpenGE.Common.Lifecycle;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Resources.Targets;

namespace OpenGE.Rendering;

/// <summary>
/// Executes backend-independent rendering commands within explicit frames.
/// </summary>
/// <param name="identifier">The nonempty backend identifier.</param>
public abstract class RenderBackend(string identifier) : Destroyable, IIdentifiable<string>
{
    private bool _frameStarted;

    /// <inheritdoc />
    public string Identifier { get; } =
        !string.IsNullOrWhiteSpace(identifier)
            ? identifier
            : throw new ArgumentException(
                "Render backend identifier cannot be empty.",
                nameof(identifier)
            );

    /// <summary>
    /// Acquires the resources needed to begin a frame.
    /// </summary>
    protected abstract void OnBeginFrame();

    /// <summary>
    /// Finishes and presents the active frame.
    /// </summary>
    protected abstract void OnEndFrame();

    /// <summary>
    /// Selects the rendering destination for the next commands.
    /// </summary>
    /// <param name="target">
    /// The offscreen rendering target, or null to render to the window.
    /// </param>
    /// <remarks>
    /// The target is borrowed and is not owned by the backend.
    /// Implementations must preserve previously rendered contents unless
    /// an explicit command requests otherwise.
    /// </remarks>
    protected abstract void OnBeginRender(RenderTarget? target);

    /// <summary>
    /// Executes one command with the context of its rendering space.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="context">
    /// The borrowed camera/viewport context, or null for context-free commands.
    /// </param>
    protected abstract void OnRender(IRenderCommand command, IRenderContext? context);

    /// <summary>
    /// Begins a frame. Nested frames are rejected.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A frame is already active.
    /// </exception>
    public void BeginFrame()
    {
        ThrowIfDestroyed();

        if (_frameStarted)
            throw new InvalidOperationException("A rendering frame is already active.");

        OnBeginFrame();
        _frameStarted = true;
    }

    /// <summary>
    /// Ends the current frame, if one is active.
    /// </summary>
    public void EndFrame()
    {
        ThrowIfDestroyed();

        if (!_frameStarted)
            return;

        try
        {
            OnEndFrame();
        }
        finally
        {
            _frameStarted = false;
        }
    }

    /// <summary>
    /// Executes commands in enumeration order on the specified rendering target.
    /// </summary>
    /// <param name="commands">
    /// The rendering commands to execute.
    /// </param>
    /// <param name="target">
    /// The offscreen rendering target, or null to render to the window.
    /// </param>
    /// <param name="context">
    /// The context of the space that produced the commands.
    /// </param>
    /// <remarks>
    /// The rendering destination is selected once per invocation.
    /// Commands execute in enumeration order.
    ///
    /// Render2D commands require a context. Clear commands do not.
    /// The backend does not own the target or the rendering context.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No frame is active.
    /// </exception>
    public void Render(
        IEnumerable<IRenderCommand> commands,
        RenderTarget? target = null,
        IRenderContext? context = null
    )
    {
        ThrowIfDestroyed();
        ArgumentNullException.ThrowIfNull(commands);

        if (!_frameStarted)
            throw new InvalidOperationException("BeginFrame must be called before Render.");

        if (target is { Destroyed: true })
            throw new ObjectDisposedException(nameof(target));

        OnBeginRender(target);

        foreach (var command in commands)
            OnRender(command, context);
    }
}
