using Mirage.Common.Events;
using Mirage.Common.Lifecycle;
using Mirage.Common.Primitives;

namespace Mirage.Common;

/// <summary>
/// Represents the current lifecycle state of an engine.
/// </summary>
public enum EngineState
{
    /// <summary>
    /// Indicates that the engine is idle and not currently running.
    /// </summary>
    Idle,

    /// <summary>
    /// Indicates that the engine is currently starting.
    /// </summary>
    Starting,

    /// <summary>
    /// Indicates that the engine is running.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates that the engine is currently stopping.
    /// </summary>
    Stopping,
}

/// <summary>
/// Coordinates the lifecycle, dependency resolution and its registered modules.
/// </summary>
/// <remarks>
/// An engine manages its modules by resolving their dependencies, injecting their
/// shared context, and starting and stopping them in dependency order.
/// It owns and destroys its registered modules. Passing modules to the constructor or
/// returning them from composition transfers ownership; do not register them in another engine.
/// </remarks>
public abstract class Engine : Destroyable
{
    private readonly Dictionary<string, Module> _modules = [];
    private readonly Store<EngineState> _state = new(EngineState.Idle);
    private readonly Preparator _preparator = new();
    private bool _injected;
    private IReadOnlyList<Module>? _moduleOrder;

    /// <summary>
    /// Initializes a new instance of the <see cref="Engine"/> class.
    /// </summary>
    /// <param name="modules">
    /// The initial modules to register.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple modules have the same identifier.
    /// </exception>
    protected Engine(params IEnumerable<Module> modules)
    {
        foreach (var module in modules)
        {
            if (!_modules.TryAdd(module.Identifier, module))
            {
                throw new InvalidOperationException(
                    $"Duplicate module identifier found: '{module.Identifier}'."
                );
            }
        }

        Modules = _modules.AsReadOnly();
        State = _state;
    }

    /// <summary>
    /// Gets the modules registered in the engine, indexed by identifier.
    /// </summary>
    public IReadOnlyDictionary<string, Module> Modules { get; }

    /// <summary>
    /// Gets a read-only view of the engine's current lifecycle state.
    /// </summary>
    public IReadOnlyStore<EngineState> State { get; }

    private void ComposeContents()
    {
        foreach (var module in Compose())
        {
            if (!_modules.TryAdd(module.Identifier, module))
            {
                if (!ReferenceEquals(_modules[module.Identifier], module) && !module.Destroyed)
                    module.Destroy();

                throw new InvalidOperationException(
                    $"Duplicate module identifier found: '{module.Identifier}'."
                );
            }
        }
    }

    private void ConfigureContents()
    {
        Configure();
    }

    private IReadOnlyList<Module> ResolveModuleOrder()
    {
        Dictionary<string, Module> modulesByIdentifier = [];

        foreach (var module in _modules.Values)
            modulesByIdentifier.Add(module.Identifier, module);

        List<Module> sortedModules = [];
        HashSet<string> visiting = [];
        HashSet<string> visited = [];

        foreach (
            var module in _modules.Values.Where(module => !visited.Contains(module.Identifier))
        )
            Resolve(module, []);

        return sortedModules;

        void Resolve(Module module, List<string> dependencyPath)
        {
            if (visiting.Contains(module.Identifier))
            {
                var cyclePath = string.Join(" -> ", [.. dependencyPath, module.Identifier]);

                throw new InvalidOperationException(
                    $"Circular dependency detected in modules: {cyclePath}"
                );
            }

            if (visited.Contains(module.Identifier))
                return;

            visiting.Add(module.Identifier);
            dependencyPath.Add(module.Identifier);

            foreach (var dependency in module.Dependencies)
            {
                if (!modulesByIdentifier.TryGetValue(dependency, out var dependencyModule))
                    throw new InvalidOperationException(
                        $"Module '{module.Identifier}' requires missing dependency '{dependency}'"
                    );

                Resolve(dependencyModule, dependencyPath);
            }

            dependencyPath.RemoveAt(dependencyPath.Count - 1);

            visiting.Remove(module.Identifier);
            visited.Add(module.Identifier);
            sortedModules.Add(module);
        }
    }

    private static void RollbackStartedModules(IReadOnlyList<Module> startedModules)
    {
        for (var index = startedModules.Count - 1; index >= 0; index--)
        {
            var module = startedModules[index];

            try
            {
                module.Stop();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    /// <summary>
    /// Composes the modules managed by this object.
    /// </summary>
    /// <returns>The modules to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once when the engine first starts, before dependency resolution and injection.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// The engine owns and destroys its registered modules.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<Module> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition, before startup or first use.
    /// </summary>
    /// <remarks>
    /// All constructor-provided and composed modules are available through
    /// <see cref="Modules"/> and <see cref="Require{TModule}"/>. Configuration occurs
    /// before dependency resolution, injection, and module startup.
    /// This hook is invoked at most once, including across later lifecycle cycles.
    /// If configuration throws, later lifecycle calls reject further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        var currentState = _state.Get();

        if (currentState != EngineState.Idle)
            throw new InvalidOperationException(
                $"Engine cannot be destroyed while in state '{currentState}'"
            );

        foreach (var module in (_moduleOrder ?? _modules.Values.ToArray()).Reverse())
            module.Destroy();

        _modules.Clear();

        _state.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Called when the engine has successfully started all modules.
    /// </summary>
    /// <remarks>
    /// Override this method to perform engine-specific startup logic.
    /// </remarks>
    protected virtual void OnStart() { }

    /// <summary>
    /// Called when the engine has successfully stopped all running modules.
    /// </summary>
    /// <remarks>
    /// Override this method to perform engine-specific shutdown logic.
    /// </remarks>
    protected virtual void OnStop() { }

    /// <summary>
    /// Gets a registered module of the specified type.
    /// </summary>
    /// <typeparam name="TModule">
    /// The type of the module to retrieve.
    /// </typeparam>
    /// <returns>
    /// The registered module of the specified type.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no module or more than one module matches the specified type.
    /// </exception>
    protected TModule Require<TModule>()
        where TModule : Module
    {
        return _modules.Values.OfType<TModule>().Single();
    }

    /// <summary>
    /// Starts the engine and all registered modules.
    /// </summary>
    /// <remarks>
    /// Module dependencies are resolved before startup. Each module receives
    /// its dependencies through dependency injection before its startup logic
    /// is executed.
    ///
    /// After all modules have started successfully, the engine transitions to
    /// <see cref="EngineState.Running"/> and <see cref="OnStart"/> is invoked.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the engine has already been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the engine is not idle, a module dependency is missing, or
    /// module dependencies contain a circular reference.
    /// </exception>
    public void Start()
    {
        ThrowIfDestroyed();

        var currentState = _state.Get();

        if (currentState != EngineState.Idle)
        {
            throw new InvalidOperationException(
                $"Engine cannot be started from state '{currentState}'."
            );
        }

        _state.Set(EngineState.Starting);

        List<Module> startedModules = [];

        try
        {
            _preparator.Ensure(ComposeContents, ConfigureContents);

            var sortedModules = ResolveModuleOrder();
            _moduleOrder = sortedModules;

            if (!_injected)
            {
                ModuleContext context = new() { Modules = new ModuleContainer(sortedModules) };

                foreach (var module in sortedModules)
                    module.Inject(context);

                _injected = true;
            }

            foreach (var module in sortedModules)
            {
                module.Start();
                startedModules.Add(module);
            }

            _state.Set(EngineState.Running);

            OnStart();
        }
        catch
        {
            RollbackStartedModules(startedModules);

            _state.Set(EngineState.Idle);

            throw;
        }
    }

    /// <summary>
    /// Stops the engine and all running modules in reverse dependency order.
    /// </summary>
    /// <remarks>
    /// The engine transitions to <see cref="EngineState.Stopping"/> before its
    /// modules are stopped.
    ///
    /// Modules are stopped in reverse dependency order. After all running
    /// modules have stopped successfully, <see cref="OnStop"/> is invoked
    /// while the engine remains in the stopping state.
    ///
    /// Once shutdown logic has completed successfully, the engine transitions
    /// to <see cref="EngineState.Idle"/>.
    ///
    /// If stopping fails, the engine returns to
    /// <see cref="EngineState.Running"/> and the exception is rethrown.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the engine has already been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the engine is not running.
    /// </exception>
    public void Stop()
    {
        ThrowIfDestroyed();

        var currentState = _state.Get();

        if (currentState != EngineState.Running)
            throw new InvalidOperationException(
                $"Engine cannot be stopped from state '{currentState}'"
            );

        _state.Set(EngineState.Stopping);

        try
        {
            var sortedModules = _moduleOrder ?? ResolveModuleOrder();

            for (var index = sortedModules.Count - 1; index >= 0; index--)
            {
                var module = sortedModules[index];

                if (module.State.Get() == ModuleState.Running)
                    module.Stop();
            }

            OnStop();

            _state.Set(EngineState.Idle);
        }
        catch (Exception)
        {
            _state.Set(EngineState.Running);

            throw;
        }
    }
}
