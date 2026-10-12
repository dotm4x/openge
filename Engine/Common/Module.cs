using Mirage.Common.Events;
using Mirage.Common.Interfaces;
using Mirage.Common.Lifecycle;

namespace Mirage.Common;

/// <summary>
/// Provides access to a collection of modules.
/// </summary>
/// <remarks>
/// A module container does not own the modules it contains and does not manage
/// their lifecycle.
/// </remarks>
/// <param name="modules">The modules exposed by the container.</param>
public sealed class ModuleContainer(IEnumerable<Module> modules)
{
    private readonly IReadOnlyList<Module> _modules = [.. modules];

    /// <summary>
    /// Gets the single module matching the specified type.
    /// </summary>
    /// <typeparam name="TModule">The type of module to retrieve.</typeparam>
    /// <returns>The matching module.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no module or more than one module matches the specified type.
    /// </exception>
    public TModule Get<TModule>()
        where TModule : Module
    {
        var modules = _modules.OfType<TModule>().Take(2).ToArray();

        return modules.Length switch
        {
            1 => modules[0],
            0 => throw new InvalidOperationException(
                $"Module '{typeof(TModule).Name}' is not available in this context."
            ),
            _ => throw new InvalidOperationException(
                $"Multiple modules matching type '{typeof(TModule).Name}' are available in this context."
            ),
        };
    }

    /// <summary>
    /// Gets the module with the specified identifier.
    /// </summary>
    /// <param name="identifier">The identifier of the module to retrieve.</param>
    /// <returns>The matching module.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no module or more than one module has the specified identifier.
    /// </exception>
    public Module Get(string identifier)
    {
        var modules = _modules.Where(module => module.Identifier == identifier).Take(2).ToArray();

        return modules.Length switch
        {
            1 => modules[0],
            0 => throw new InvalidOperationException(
                $"Module '{identifier}' is not available in this context."
            ),
            _ => throw new InvalidOperationException(
                $"Multiple modules with identifier '{identifier}' are available in this context."
            ),
        };
    }
}

internal sealed class ModuleContext
{
    public required ModuleContainer Modules { get; init; }
}

/// <summary>
/// Represents the current lifecycle state of a module.
/// </summary>
public enum ModuleState
{
    /// <summary>
    /// Indicates that the module is idle and not currently running.
    /// </summary>
    Idle,

    /// <summary>
    /// Indicates that the module is currently starting.
    /// </summary>
    Starting,

    /// <summary>
    /// Indicates that the module is running.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates that the module is currently stopping.
    /// </summary>
    Stopping,
}

/// <summary>
/// Initializes a new instance of the <see cref="Module"/> class.
/// </summary>
/// <remarks>
/// <para>
/// Represents an engine module with a managed lifecycle and declared dependencies.
/// </para>
/// Modules are initialized and managed by a <see cref="Engine"/> instance.
/// Their dependencies are injected before the module is started.
/// </remarks>
/// <param name="identifier">
/// The unique identifier of the module.
/// </param>
/// <param name="dependencies">
/// The identifiers of the modules required by this module.
/// </param>
public abstract class Module(string identifier, IEnumerable<string>? dependencies = null)
    : Destroyable,
        IIdentifiable<string>
{
    private readonly Store<ModuleState> _state = new(ModuleState.Idle);
    private bool _injected;

    /// <summary>
    /// Gets the identifiers of the modules required by this module.
    /// </summary>
    public readonly IReadOnlyList<string> Dependencies = [.. dependencies ?? []];

    /// <summary>
    /// Gets the modules injected as dependencies of this module.
    /// </summary>
    /// <remarks>
    /// The container contains only dependencies explicitly declared through
    /// <see cref="Dependencies"/>. It becomes available after the module has
    /// been injected by its owning engine. Injected dependencies are borrowed and must not
    /// be destroyed by this module.
    /// </remarks>
    protected ModuleContainer InjectedDependencies { get; private set; } = null!;

    /// <summary>
    /// Gets a read-only store for the current lifecycle state of the module.
    /// </summary>
    public IReadOnlyStore<ModuleState> State => _state;

    /// <inheritdoc />
    public string Identifier { get; } = identifier;

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        if (_state.Get() != ModuleState.Idle)
            throw new InvalidOperationException(
                $"Module '{Identifier}' cannot be destroyed while in state '{_state.Get()}'."
            );

        if (!_state.Destroyed)
            _state.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Called when the module starts.
    /// </summary>
    /// <remarks>
    /// Override this method to perform module-specific startup logic.
    /// If this hook throws, OnStop is invoked to clean up incomplete startup.
    /// </remarks>
    protected virtual void OnStart() { }

    /// <summary>
    /// Called when the module stops.
    /// </summary>
    /// <remarks>
    /// Override this method to perform module-specific shutdown logic.
    /// This hook also cleans up an incomplete OnStart and must tolerate partial initialization.
    /// Failed cleanup preserves dependencies for a later shutdown retry.
    /// </remarks>
    protected virtual void OnStop() { }

    /// <summary>
    /// Gets an injected dependency of the specified type.
    /// </summary>
    /// <typeparam name="TModule">The expected type of the dependency.</typeparam>
    /// <param name="name">The identifier of the dependency.</param>
    /// <returns>The borrowed injected dependency. Its lifetime belongs to the engine.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the module has not been injected, the dependency was not
    /// registered, or the dependency is not of the requested type.
    /// </exception>
    protected TModule Require<TModule>(string name)
        where TModule : Module
    {
        ThrowIfDestroyed();

        if (!_injected)
            throw new InvalidOperationException($"Module '{Identifier}' has not been injected.");

        Module dependency;

        try
        {
            dependency = InjectedDependencies.Get(name);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Module '{Identifier}' requires dependency '{name}', but it was not injected."
            );
        }

        if (dependency is not TModule typedModule)
            throw new InvalidOperationException(
                $"Module '{Identifier}' requires dependency '{name}' to be of type '{typeof(TModule).Name}', but it is '{dependency.GetType().Name}'."
            );

        return typedModule;
    }

    internal void Inject(ModuleContext context)
    {
        ThrowIfDestroyed();

        if (_injected)
            throw new InvalidOperationException(
                $"Module '{Identifier}' has already been injected."
            );

        List<Module> injectedDependencies =
        [
            .. Dependencies.Select(dependency => context.Modules.Get(dependency)),
        ];

        InjectedDependencies = new ModuleContainer(injectedDependencies);

        _injected = true;
    }

    internal void Start()
    {
        ThrowIfDestroyed();

        if (!_injected)
            throw new InvalidOperationException(
                $"Module '{Identifier}' has not been injected, cannot start."
            );

        var currentState = _state.Get();

        if (currentState != ModuleState.Idle)
            throw new InvalidOperationException(
                $"Module '{Identifier}' cannot be started from state '{currentState}'."
            );

        var started = false;
        var startEntered = false;

        try
        {
            _state.Set(ModuleState.Starting);
            startEntered = true;
            OnStart();
            started = true;
            _state.Set(ModuleState.Running);
        }
        catch (Exception exception)
        {
            // The engine rolls back modules whose startup completed.
            if (started)
                throw;

            List<Exception> failures = [];
            var cleanupFailed = false;
            if (startEntered)
            {
                try
                {
                    _state.Set(ModuleState.Stopping);
                }
                catch (Exception notificationFailure)
                {
                    failures.Add(notificationFailure);
                }

                try
                {
                    OnStop();
                }
                catch (Exception cleanupFailure)
                {
                    cleanupFailed = true;
                    failures.Add(cleanupFailure);
                }
            }

            try
            {
                _state.Set(cleanupFailed ? ModuleState.Running : ModuleState.Idle);
            }
            catch (Exception notificationFailure)
            {
                failures.Add(notificationFailure);
            }

            if (failures.Count != 0)
                throw new AggregateException(
                    "Module startup and recovery failed.",
                    [exception, .. failures]
                );

            throw;
        }
    }

    internal void Stop()
    {
        ThrowIfDestroyed();

        if (!_injected)
            throw new InvalidOperationException(
                $"Module '{Identifier}' has not been injected, cannot stop."
            );

        var currentState = _state.Get();

        if (currentState != ModuleState.Running)
            throw new InvalidOperationException(
                $"Module '{Identifier}' cannot be stopped from state '{currentState}'."
            );

        var stopped = false;

        try
        {
            _state.Set(ModuleState.Stopping);
            OnStop();
            stopped = true;
            _state.Set(ModuleState.Idle);
        }
        catch (Exception exception)
        {
            try
            {
                _state.Set(stopped ? ModuleState.Idle : ModuleState.Running);
            }
            catch (Exception notificationFailure)
            {
                throw new AggregateException(
                    "Module shutdown and state notification failed.",
                    exception,
                    notificationFailure
                );
            }

            throw;
        }
    }
}
