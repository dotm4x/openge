using Mirage.Common;
using Mirage.Common.Primitives;
using Mirage.Logging;
using Mirage.Scheduling;
using Mirage.Scheduling.Interfaces;
using Mirage.Windowing;
using Mirage.Windowing.Windows;

namespace Mirage.Windowing;

/// <summary>
/// Manages the lifecycle and event processing of the application's windows.
/// </summary>
/// <remarks>
/// Window event processing is driven by the scheduler through
/// <see cref="IUpdatable.Update(UpdateContext)"/>; individual windows do not own an update loop.
/// The manager owns and destroys its constructor-provided and composed windows.
/// References exposed through Windows are borrowed; do not destroy them or register them
/// with another owning manager.
/// </remarks>
public class WindowManager : Module, IUpdatable
{
    private readonly Dictionary<string, Window> _windows = [];
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the managed windows, indexed by identifier.
    /// </summary>
    public readonly IReadOnlyDictionary<string, Window> Windows;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowManager"/> class.
    /// </summary>
    /// <param name="windows">The initial windows, or <see langword="null"/> for none.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple windows have the same identifier.
    /// </exception>
    public WindowManager(IEnumerable<Window>? windows = null)
        : base("WindowManager", dependencies: ["Logger", "Scheduler"])
    {
        foreach (var window in windows ?? [])
        {
            if (!_windows.TryAdd(window.Identifier, window))
                throw new InvalidOperationException(
                    $"Duplicate window identifier found: '{window.Identifier}'."
                );
        }

        Windows = _windows.AsReadOnly();
    }

    private SourcedLogger Logger => Require<Logger>("Logger").From(Identifier);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the module is not running, including before configuration has completed.
    /// </exception>
    public virtual void Update(UpdateContext context)
    {
        ThrowIfDestroyed();

        if (State.Get() != ModuleState.Running)
            throw new InvalidOperationException(
                "The module must be running before processing updates."
            );

        foreach (var window in _windows.Values)
            window.Update(context);
    }

    private void ComposeContents()
    {
        Logger.Log("Composing module contents.", LogMessageKind.Debug);
        foreach (var window in Compose())
        {
            if (!_windows.TryAdd(window.Identifier, window))
            {
                if (!ReferenceEquals(_windows[window.Identifier], window) && !window.Destroyed)
                    window.Destroy();

                throw new InvalidOperationException(
                    $"Duplicate window identifier found: '{window.Identifier}'."
                );
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
    /// Composes the windows managed by this object.
    /// </summary>
    /// <returns>The windows to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once when the manager first starts, before windows are opened.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// The manager owns and destroys its constructor-provided and composed windows.
    /// References exposed through Windows are borrowed; do not destroy them or register them
    /// with another owning manager.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<Window> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition, before startup or first use.
    /// </summary>
    /// <remarks>
    /// All constructor-provided and composed objects are available here.
    /// This hook is invoked at most once, including across later lifecycle cycles.
    /// If configuration throws, later lifecycle calls reject further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var window in _windows.Values.ToArray())
        {
            window.Destroy();
            if (InjectedDependencies is not null)
                Logger.Log($"Destroyed window '{window.Identifier}'.", LogMessageKind.Debug);
        }

        _windows.Clear();
        if (InjectedDependencies is not null)
            Logger.Log("Module resources destroyed.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Opens every managed window. If opening a window fails, windows opened
    /// earlier during the same operation are closed in reverse order.
    /// </remarks>
    protected override void OnStart()
    {
        Logger.Log("Starting module.");
        _preparator.Ensure(ComposeContents, ConfigureContents);

        List<Window> openedWindows = [];

        try
        {
            foreach (var window in _windows.Values)
            {
                window.Open();
                openedWindows.Add(window);
                Logger.Log($"Opened window '{window.Identifier}'.");
            }
        }
        catch (Exception exception)
        {
            Logger.Log(
                $"Window startup failed; closing {openedWindows.Count} opened windows: {exception.Message}",
                LogMessageKind.Warn
            );
            for (var index = openedWindows.Count - 1; index >= 0; index--)
                openedWindows[index].Close();

            throw;
        }
        Logger.Log("Module started.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Closes all open windows in reverse registration order.
    /// </remarks>
    protected override void OnStop()
    {
        foreach (var window in _windows.Values.Reverse())
        {
            if (window.Opened.Get())
            {
                window.Close();
                Logger.Log($"Closed window '{window.Identifier}'.");
            }
        }
        Logger.Log("Module stopped.");
    }
}
