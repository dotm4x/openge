using System.Numerics;
using OpenGE.Common;
using OpenGE.Common.Collections;
using OpenGE.Common.Events;
using OpenGE.Common.Lifecycle;
using OpenGE.Common.Primitives;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Primitives;
using OpenGE.Graphics.Resources.Targets;
using OpenGE.Logging;
using OpenGE.Rendering.Spaces;
using OpenGE.Scheduling;
using OpenGE.Scheduling.Interfaces;
using OpenGE.Windowing;

namespace OpenGE.Rendering;

/// <summary>
/// Manages rendering backends and spaces used to produce frames for one window.
/// </summary>
/// <remarks>
/// The renderer coordinates the production and execution of rendering commands
/// for its associated window.
///
/// Rendering spaces collect backend-independent commands from their renderable
/// objects using the active camera and a viewport derived from their rendering
/// target. Spaces without an explicit target render to the associated window.
/// The active rendering backend executes commands in space priority order.
///
/// The renderer owns and destroys its registered rendering backends and spaces.
/// Construction and composition transfer ownership of backends and spaces
/// to the renderer; registered sets are exposed as read-only views.
///
/// Frames are only rendered while the associated window is open.
///
/// The associated window is borrowed and is never destroyed by the renderer.
/// </remarks>
public class Renderer : Module, IUpdatable
{
    private readonly Store<RenderBackend> _activeBackend;
    private readonly IdentifiableSet<string, RenderBackend> _backends = [];
    private readonly IdentifiableSet<string, RenderSpace> _spaces = [];
    private readonly List<RenderSpace> _spaceOrder = [];

    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the identifiable set of rendering backends managed by the renderer.
    /// </summary>
    /// <remarks>
    /// The renderer owns and destroys its registered backends.
    /// The set is read-only because the active backend may reference registered
    /// entries; register additional backends through the constructor or composition.
    /// </remarks>
    public IReadOnlyIdentifiableSet<string, RenderBackend> Backends => _backends;

    /// <summary>
    /// Gets the color used to clear the rendering target at the start of each frame.
    /// </summary>
    public readonly Store<Color> ClearColor;

    /// <summary>
    /// Gets the identifiable set of rendering spaces managed by the renderer.
    /// </summary>
    /// <remarks>
    /// The renderer owns and destroys its registered spaces.
    /// Spaces with the same priority are rendered in registration order.
    /// Do not register a space owned elsewhere.
    /// </remarks>
    public IReadOnlyIdentifiableSet<string, RenderSpace> Spaces => _spaces;

    /// <summary>
    /// Gets the window associated with this renderer.
    /// </summary>
    /// <remarks>
    /// The window is borrowed and is never destroyed by the renderer.
    /// Its current size determines the viewport used for each rendering update.
    /// </remarks>
    public readonly Window Window;

    /// <summary>
    /// Initializes a new instance of the <see cref="Renderer"/> class.
    /// </summary>
    /// <param name="index">
    /// The index used to distinguish the module identifier.
    /// </param>
    /// <param name="initial">
    /// The initially selected rendering backend.
    /// </param>
    /// <param name="window">
    /// The window associated with this renderer.
    /// </param>
    /// <param name="clearColor">
    /// The color used to clear the rendering target at the start of each frame.
    /// </param>
    /// <param name="backends">
    /// Additional rendering backends to register, or <see langword="null"/>
    /// for no additional backends.
    /// </param>
    /// <param name="spaces">
    /// The initial rendering spaces, or <see langword="null"/> for no spaces.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initial backends or spaces contain duplicate identifiers.
    /// </exception>
    public Renderer(
        int index,
        RenderBackend initial,
        Window window,
        Color? clearColor = null,
        IEnumerable<RenderBackend>? backends = null,
        IEnumerable<RenderSpace>? spaces = null
    )
        : base($"Renderer-{index}", ["Logger"])
    {
        _activeBackend = new Store<RenderBackend>(initial);
        ActiveBackend = _activeBackend;

        Window = window;
        ClearColor = new Store<Color>(clearColor ?? Color.Black);

        _spaces.OnAdd.Connect(OnSpaceAdded);
        _spaces.OnRemove.Connect(OnSpaceRemoved);

        _backends.Add(initial);

        foreach (var backend in backends ?? [])
            _backends.Add(backend);

        foreach (var space in spaces ?? [])
            _spaces.Add(space);
    }

    private void OnSpaceAdded(RenderSpace space)
    {
        if (!_spaceOrder.Contains(space))
            _spaceOrder.Add(space);
    }

    private void OnSpaceRemoved(RenderSpace space)
    {
        _spaceOrder.Remove(space);
    }

    private SourcedLogger Logger => Require<Logger>("Logger").From(Identifier);

    /// <summary>
    /// Gets the currently selected rendering backend.
    /// </summary>
    /// <remarks>
    /// Changes made through <see cref="SwitchBackend(string)"/> are published
    /// through this store.
    /// </remarks>
    public IReadOnlyStore<RenderBackend> ActiveBackend { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Frames are rendered only while the associated window is open.
    ///
    /// The window is cleared at the start of every frame. Each offscreen target is
    /// cleared once, before the first space that renders to it.
    ///
    /// Frame completion is attempted even when command collection or rendering fails.
    /// If both fail, the rendering error is the first inner exception of an aggregate.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The module is not running, or a rendering space does not define a camera.
    /// </exception>
    public virtual void Update(UpdateContext context)
    {
        ThrowIfDestroyed();

        if (State.Get() != ModuleState.Running)
        {
            throw new InvalidOperationException("The module must be running before rendering.");
        }

        if (!Window.Opened.Get())
            return;

        var backend = _activeBackend.Get();

        backend.BeginFrame();
        Exception? renderingFailure = null;

        try
        {
            ClearCommand clear = new(ClearColor.Get());
            HashSet<RenderTarget> preparedTargets = [];

            backend.Render([clear]);

            foreach (var space in _spaceOrder.OrderBy(space => space.Priority))
            {
                var camera = space.Camera.Get();

                if (camera is null)
                {
                    throw new InvalidOperationException(
                        $"Rendering space '{space.Identifier}' requires a camera."
                    );
                }

                var target = space.Target.Get();

                if (target is not null && preparedTargets.Add(target))
                    backend.Render([clear], target);

                var viewport = new Viewport(Vector2.Zero, target?.Size ?? Window.Size.Get());

                var renderContext = new RenderContext(context.DeltaTime, camera, viewport);

                backend.Render(space.Collect(renderContext), target, renderContext);
            }
        }
        catch (Exception exception)
        {
            renderingFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                backend.EndFrame();
            }
            catch (Exception frameFailure) when (renderingFailure is not null)
            {
                throw new AggregateException(
                    "Rendering and frame completion failed.",
                    renderingFailure,
                    frameFailure
                );
            }
        }
    }

    private void ComposeContents()
    {
        Logger.Log("Composing module contents.", LogMessageKind.Debug);

        foreach (var backend in ComposeBackends())
        {
            try
            {
                _backends.Add(backend);
            }
            catch
            {
                if (!_backends.Contains(backend) && !backend.Destroyed)
                    backend.Destroy();

                throw;
            }
        }

        foreach (var space in ComposeSpaces())
        {
            try
            {
                _spaces.Add(space);
            }
            catch
            {
                if (!_spaces.Contains(space) && !space.Destroyed)
                    space.Destroy();

                throw;
            }
        }

        Logger.Log("Composition completed.", LogMessageKind.Debug);
    }

    private void ConfigureContents()
    {
        Logger.Log("Configuring module.", LogMessageKind.Debug);

        Configure();

        Logger.Log("Configuration completed.", LogMessageKind.Debug);
    }

    /// <summary>
    /// Composes the additional rendering backends managed by this renderer.
    /// </summary>
    /// <remarks>
    /// Composition occurs once when the renderer first starts.
    /// The initial and constructor-provided backends are registered before
    /// composed backends.
    ///
    /// The renderer owns and destroys all composed backends.
    /// The initially selected backend should not be returned from this method
    /// because it is already registered by the constructor.
    ///
    /// All backends are composed before rendering spaces and before configuration
    /// occurs.
    ///
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    /// <returns>
    /// The rendering backends to register, in enumeration order.
    /// </returns>
    protected virtual IEnumerable<RenderBackend> ComposeBackends()
    {
        yield break;
    }

    /// <summary>
    /// Composes the additional rendering spaces managed by this renderer.
    /// </summary>
    /// <remarks>
    /// Composition occurs once when the renderer first starts.
    /// Constructor-provided spaces are registered before composed spaces.
    ///
    /// The renderer owns and destroys all composed spaces.
    /// All rendering backends are registered before space composition begins,
    /// and all spaces are registered before configuration occurs.
    ///
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    /// <returns>
    /// The rendering spaces to register, in enumeration order.
    /// </returns>
    protected virtual IEnumerable<RenderSpace> ComposeSpaces()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition.
    /// </summary>
    /// <remarks>
    /// All registered rendering backends and spaces are available when this
    /// hook is invoked.
    ///
    /// This hook is invoked at most once, including across later lifecycle cycles.
    /// If configuration throws, later lifecycle calls reject further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        var state = State.Get();
        if (state != ModuleState.Idle)
            throw new InvalidOperationException(
                $"Renderer cannot be destroyed while in state '{state}'."
            );

        foreach (var space in _spaces.ToArray())
        {
            space.Destroy();

            if (InjectedDependencies is not null)
            {
                Logger.Log(
                    $"Destroyed rendering space '{space.Identifier}'.",
                    LogMessageKind.Debug
                );
            }
        }

        _spaces.Destroy();
        _spaceOrder.Clear();
        ClearColor.Destroy();

        foreach (var backend in _backends.ToArray())
        {
            backend.Destroy();

            if (InjectedDependencies is not null)
            {
                Logger.Log(
                    $"Destroyed rendering backend '{backend.Identifier}'.",
                    LogMessageKind.Debug
                );
            }
        }

        _backends.Destroy();

        _activeBackend.Destroy();

        if (InjectedDependencies is not null)
            Logger.Log("Module resources destroyed.");

        base.OnDestroy();
    }

    /// <inheritdoc />
    protected override void OnStart()
    {
        Logger.Log("Starting module.");

        _preparator.Ensure(ComposeContents, ConfigureContents);

        Logger.Log(
            $"Module started with {_backends.Count} rendering backends and "
                + $"{_spaces.Count} rendering spaces for window '{Window.Identifier}'."
        );
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        Logger.Log("Module stopped.");
    }

    /// <summary>
    /// Selects the rendering backend with the specified identifier.
    /// </summary>
    /// <param name="identifier">
    /// The identifier of the rendering backend to select.
    /// </param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the renderer has been destroyed.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="identifier"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no rendering backend with the specified identifier is registered.
    /// </exception>
    public void SwitchBackend(string identifier)
    {
        ThrowIfDestroyed();
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (!_backends.TryGetValue(identifier, out var backend))
        {
            throw new InvalidOperationException(
                $"Rendering backend '{identifier}' does not exist."
            );
        }

        if (ReferenceEquals(_activeBackend.Get(), backend))
            return;

        _activeBackend.Set(backend);

        if (InjectedDependencies is not null)
        {
            Logger.Log($"Selected rendering backend '{identifier}'.", LogMessageKind.Debug);
        }
    }
}
