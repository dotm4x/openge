using Mirage.Common;
using Mirage.Common.Collections;
using Mirage.Common.Events;
using Mirage.Common.Primitives;
using Mirage.Logging;

namespace Mirage.Noding;

/// <summary>
/// Coordinates node lifecycles between roots.
/// </summary>
/// <remarks>
/// The manager owns and destroys every registered root. Its declared module
/// dependencies are exposed to managed nodes through their dependency context.
/// </remarks>
public class NodeManager : Module
{
    private readonly Store<Node> _activeRoot;
    private readonly Preparator _preparator = new();
    private NodeContext? _nodeContext;
    private bool _restoringRoots;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeManager"/> class.
    /// </summary>
    /// <param name="initial">The initially selected root.</param>
    /// <param name="roots">
    /// Additional roots to register; <see langword="null"/> means no additional roots.
    /// </param>
    /// <param name="dependencies">
    /// The identifiers of the modules that managed nodes may access.
    /// </param>
    public NodeManager(
        Node initial,
        IEnumerable<Node>? roots = null,
        IEnumerable<string>? dependencies = null
    )
        : base("NodeManager", ["Logger", .. dependencies ?? []])
    {
        _activeRoot = new Store<Node>(initial);
        ActiveRoot = _activeRoot;

        Roots.OnAdd.Connect(OnRootAdded);
        Roots.OnRemove.Connect(OnRootRemoved);
        Roots.OnUpdate.Connect(OnRootUpdated);
        Roots.OnClear.Connect(OnRootsClearing);

        try
        {
            Roots.Add(initial.Name.Get(), initial);

            foreach (var root in roots ?? [])
                Roots.Add(root.Name.Get(), root);
        }
        catch
        {
            foreach (var root in Roots.Values)
                root.RootOwner = null;

            Roots.Destroy();
            _activeRoot.Destroy();
            throw;
        }
    }

    private SourcedLogger Logger => Require<Logger>("Logger").From(Identifier);

    /// <summary>
    /// Gets the currently selected root.
    /// </summary>
    /// <remarks>
    /// The selected root is only loaded while the node manager is running.
    /// Changes made by <see cref="Switch(string)"/> are published through this store.
    /// </remarks>
    public IReadOnlyStore<Node> ActiveRoot { get; }

    /// <summary>
    /// Gets the roots registered in the node manager, indexed by stable identifiers.
    /// </summary>
    /// <remarks>
    /// A root identifier is initially obtained from its name but does not
    /// automatically change if the node is renamed.
    /// Registration transfers ownership to this manager. Removing or replacing
    /// an inactive root returns ownership of the old root to the caller without destroying it.
    /// A root cannot simultaneously belong to another manager or parent.
    /// </remarks>
    public ReactiveDictionary<string, Node> Roots { get; } = [];

    private void ComposeContents()
    {
        Logger.Log("Composing module contents.", LogMessageKind.Debug);
        foreach (var root in Compose())
        {
            try
            {
                Roots.Add(root.Name.Get(), root);
            }
            catch
            {
                if (root.RootOwner is null && root.Parent.Get() is null && !root.Destroyed)
                    root.Destroy();

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

    private void OnRootAdded(KeyValuePair<string, Node> entry)
    {
        if (_restoringRoots)
            return;

        try
        {
            ValidateRoot(entry.Key, entry.Value);

            if (_nodeContext is not null)
            {
                entry.Value.Inject(_nodeContext);
                Logger.Log($"Registered root '{entry.Key}'.", LogMessageKind.Debug);
            }

            entry.Value.RootOwner = this;
        }
        catch
        {
            _restoringRoots = true;

            try
            {
                Roots.Remove(entry.Key);
            }
            finally
            {
                _restoringRoots = false;
            }

            throw;
        }
    }

    private void OnRootRemoved(KeyValuePair<string, Node> entry)
    {
        if (_restoringRoots)
            return;

        if (!ReferenceEquals(entry.Value, _activeRoot.Get()))
        {
            entry.Value.RootOwner = null;
            return;
        }

        _restoringRoots = true;

        try
        {
            Roots.Add(entry.Key, entry.Value);
        }
        finally
        {
            _restoringRoots = false;
        }

        throw new InvalidOperationException(
            $"Cannot remove active root '{entry.Key}'. Switch roots first."
        );
    }

    private void OnRootUpdated(
        (KeyValuePair<string, Node> Previous, KeyValuePair<string, Node> Current) change
    )
    {
        if (_restoringRoots)
            return;

        if (ReferenceEquals(change.Previous.Value, change.Current.Value))
            return;

        try
        {
            if (ReferenceEquals(change.Previous.Value, _activeRoot.Get()))
            {
                throw new InvalidOperationException(
                    $"Cannot replace active root '{change.Previous.Key}'. Switch roots first."
                );
            }

            ValidateRoot(change.Current.Key, change.Current.Value);

            if (_nodeContext is not null)
            {
                change.Current.Value.Inject(_nodeContext);
                Logger.Log($"Replaced root '{change.Current.Key}'.", LogMessageKind.Debug);
            }

            change.Previous.Value.RootOwner = null;
            change.Current.Value.RootOwner = this;
        }
        catch
        {
            _restoringRoots = true;

            try
            {
                Roots[change.Previous.Key] = change.Previous.Value;
            }
            finally
            {
                _restoringRoots = false;
            }

            throw;
        }
    }

    private void OnRootsClearing(Unit _)
    {
        if (_restoringRoots || Roots.Count == 0)
            return;

        throw new InvalidOperationException(
            "Cannot clear node manager roots while an active root is registered."
        );
    }

    private void SwitchLoadedRoot(Node current, Node next)
    {
        ValidateLoadableRoot(next);

        current.Unload();

        try
        {
            next.Load();
            _activeRoot.Set(next);

            Logger.Log(
                $"NodeManager switched active root from '{current.Name.Get()}' to '{next.Name.Get()}'."
            );
        }
        catch (Exception exception)
        {
            Logger.Log(
                $"Failed to load root '{next.Name.Get()}'; restoring '{current.Name.Get()}': {exception.Message}",
                LogMessageKind.Warn
            );
            if (!current.Loaded)
                current.Load();

            throw;
        }
    }

    private static void ValidateLoadableRoot(Node root)
    {
        if (root.Parent.Get() is not null)
        {
            throw new InvalidOperationException(
                $"{root} cannot be loaded as a root because it has a parent."
            );
        }
    }

    private void ValidateRoot(string identifier, Node root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (root.Destroyed)
            throw new InvalidOperationException("A destroyed node cannot be registered as a root.");

        if (root.RootOwner is not null && !ReferenceEquals(root.RootOwner, this))
            throw new InvalidOperationException(
                "The root is already owned by another node manager."
            );

        if (root.Parent.Get() is not null)
        {
            throw new InvalidOperationException(
                $"{root} cannot be registered as a root because it has a parent."
            );
        }

        if (root.Loaded)
        {
            throw new InvalidOperationException(
                $"{root} cannot be registered because it is already loaded."
            );
        }

        foreach (var entry in Roots)
        {
            if (entry.Key == identifier)
                continue;

            if (ReferenceEquals(entry.Value, root))
            {
                throw new InvalidOperationException(
                    $"{root} is already registered as root '{entry.Key}'."
                );
            }
        }
    }

    /// <summary>
    /// Composes the additional roots managed by this node manager.
    /// </summary>
    /// <returns>The roots to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once when the node manager first starts.
    /// The initial and constructor-provided roots are registered before composed roots.
    /// All composed roots are registered before configuration occurs.
    /// Registered roots receive the node manager's injected module dependencies
    /// before they are loaded.
    /// The node manager owns and destroys all registered roots.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<Node> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition, before startup.
    /// </summary>
    /// <remarks>
    /// The initial, constructor-provided, and composed roots are available here.
    /// This hook is invoked at most once, including across later lifecycle cycles.
    /// If configuration throws, later lifecycle calls reject further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();

        var active = _activeRoot.Get();

        if (active.Loaded)
            active.Unload();

        foreach (var root in Roots.ToArray())
        {
            root.Value.Destroy();
            root.Value.RootOwner = null;
            if (InjectedDependencies is not null)
                Logger.Log($"Destroyed root '{root.Key}'.", LogMessageKind.Debug);
        }

        _restoringRoots = true;

        try
        {
            Roots.Destroy();
        }
        finally
        {
            _restoringRoots = false;
        }

        _activeRoot.Destroy();
        if (InjectedDependencies is not null)
            Logger.Log("Module resources destroyed.");
    }

    /// <inheritdoc />
    protected override void OnStart()
    {
        Logger.Log("Starting module.");
        _preparator.Ensure(ComposeContents, ConfigureContents);

        _nodeContext ??= new NodeContext(InjectedDependencies);

        foreach (var root in Roots.Values)
            root.Inject(_nodeContext);

        var active = _activeRoot.Get();

        ValidateLoadableRoot(active);
        active.Load();

        Logger.Log($"NodeManager loaded active root '{active.Name.Get()}'.");
        Logger.Log("Module started.");
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        var active = _activeRoot.Get();

        if (active.Loaded)
            active.Unload();

        Logger.Log($"NodeManager unloaded active root '{active.Name.Get()}'.");
        Logger.Log("Module stopped.");
    }

    /// <summary>
    /// Selects the root with the specified identifier.
    /// </summary>
    /// <param name="identifier">The identifier of the root to select.</param>
    /// <remarks>
    /// When the node manager is idle, the root is selected without being loaded.
    /// When the node manager is running, the current root is unloaded and the selected
    /// root is loaded immediately.
    /// </remarks>
    /// <exception cref="Mirage.Common.Lifecycle.DestroyedObjectException">
    /// Thrown when the manager has been destroyed.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="identifier"/> is empty or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requested root does not exist, cannot be loaded, or the
    /// node manager is currently starting or stopping.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the node manager has an unrecognized module state.
    /// </exception>
    public void Switch(string identifier)
    {
        ThrowIfDestroyed();
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (!Roots.TryGetValue(identifier, out var next))
        {
            throw new InvalidOperationException($"Root '{identifier}' does not exist.");
        }

        var current = _activeRoot.Get();

        if (ReferenceEquals(current, next))
            return;

        var state = State.Get();

        switch (state)
        {
            case ModuleState.Idle:
                ValidateLoadableRoot(next);
                _activeRoot.Set(next);
                if (InjectedDependencies is not null)
                    Logger.Log($"Selected inactive root '{identifier}'.", LogMessageKind.Debug);
                break;

            case ModuleState.Running:
                SwitchLoadedRoot(current, next);
                break;

            case ModuleState.Starting:
            case ModuleState.Stopping:
                throw new InvalidOperationException(
                    $"Cannot switch roots while node manager is in state '{state}'."
                );

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    "Unknown module state."
                );
        }
    }
}
