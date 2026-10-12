using System.Numerics;
using Mirage.Common;
using Mirage.Common.Collections;
using Mirage.Common.Events;
using Mirage.Common.Lifecycle;
using Mirage.Common.Primitives;
using Mirage.Graphics.Commands;
using Mirage.Graphics.Interfaces;
using Mirage.Graphics.Primitives;
using Mirage.Logging;
using Mirage.Rendering.Spaces;
using Mirage.Scheduling;
using Mirage.Scheduling.Interfaces;
using Mirage.Windowing;

namespace Mirage.Rendering;

/// <summary>
/// Manages rendering backends and spaces used to produce frames for one window.
/// </summary>
/// <remarks>
/// The renderer coordinates the production and execution of rendering commands
/// for its associated window.
///
/// Rendering spaces collect backend-independent commands from their renderable
/// objects using the active camera and a viewport derived from the current
/// window size. The active rendering backend then executes those commands in
/// space priority order.
///
/// The renderer owns and destroys its registered rendering backends and spaces.
/// Registration transfers ownership of backends and spaces to the renderer.
///
/// The associated window is borrowed and is never destroyed by the renderer.
/// </remarks>
public class Renderer : Module, IUpdatable
{
    private readonly Store<RenderBackend> _activeBackend;

    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the identifiable set of rendering backends managed by the renderer.
    /// </summary>
    /// <remarks>
    /// The renderer owns and destroys its registered backends.
    /// Registration transfers ownership to the renderer. Removing or clearing
    /// entries returns ownership to the caller without destroying them.
    /// Do not register a backend owned elsewhere.
    /// </remarks>
    public readonly IdentifiableSet<string, RenderBackend> Backends = [];

    /// <summary>
    /// Gets the color used to clear the rendering target at the start of each frame.
    /// </summary>
    public readonly Store<Color> ClearColor;

    /// <summary>
    /// Gets the identifiable set of rendering spaces managed by the renderer.
    /// </summary>
    /// <remarks>
    /// The renderer owns and destroys its registered spaces.
    /// Registration transfers ownership to the renderer. Removing or clearing
    /// entries returns ownership to the caller without destroying them.
    /// Do not register a space owned elsewhere.
    /// </remarks>
    public readonly IdentifiableSet<string, RenderSpace> Spaces = [];

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
        ClearColor = new Store<Color>(clearColor ?? Color.White);

        Backends.Add(initial);

        foreach (var backend in backends ?? [])
            Backends.Add(backend);

        foreach (var space in spaces ?? [])
            Spaces.Add(space);
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
    /// <remarks>Frame completion is attempted even when command collection or rendering fails.
    /// If both fail, the rendering error is the first inner exception of an aggregate.</remarks>
    public virtual void Update(UpdateContext context)
    {
        ThrowIfDestroyed();

        if (State.Get() != ModuleState.Running)
        {
            throw new InvalidOperationException("The module must be running before rendering.");
        }

        var backend = _activeBackend.Get();
        var viewport = new Viewport(Vector2.Zero, Window.Size.Get());

        backend.BeginFrame();
        Exception? renderingFailure = null;

        try
        {
            backend.Render([new ClearCommand(ClearColor.Get())]);

            foreach (var space in Spaces.OrderBy(space => space.Priority))
            {
                var renderContext = new RenderContext(
                    context.DeltaTime,
                    space.Camera.Get(),
                    viewport
                );

                backend.Render(space.Collect(renderContext), renderContext);
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
                Backends.Add(backend);
            }
            catch
            {
                if (!Backends.Contains(backend) && !backend.Destroyed)
                    backend.Destroy();

                throw;
            }
        }

        foreach (var space in ComposeSpaces())
        {
            try
            {
                Spaces.Add(space);
            }
            catch
            {
                if (!Spaces.Contains(space) && !space.Destroyed)
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
    /// <returns>
    /// The rendering backends to register, in enumeration order.
    /// </returns>
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
    protected virtual IEnumerable<RenderBackend> ComposeBackends()
    {
        yield break;
    }

    /// <summary>
    /// Composes the additional rendering spaces managed by this renderer.
    /// </summary>
    /// <returns>
    /// The rendering spaces to register, in enumeration order.
    /// </returns>
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

        foreach (var space in Spaces.ToArray())
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

        Spaces.Destroy();
        ClearColor.Destroy();

        foreach (var backend in Backends.ToArray())
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

        Backends.Destroy();

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
            $"Module started with {Backends.Count} rendering backends and "
                + $"{Spaces.Count} rendering spaces for window '{Window.Identifier}'."
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

        if (!Backends.TryGetValue(identifier, out var backend))
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
