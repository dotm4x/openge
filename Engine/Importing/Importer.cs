using Mirage.Common;
using Mirage.Common.Collections;
using Mirage.Common.Interfaces;
using Mirage.Common.Primitives;
using Mirage.Logging;

namespace Mirage.Importing;

/// <summary>
/// Initializes a new instance of the <see cref="ImportContext"/> class.
/// </summary>
/// <remarks>
/// Describes a resource importing operation.
/// </remarks>
/// <param name="Identifier">
/// The normalized resource identifier relative to the importer root.
/// </param>
/// <param name="Path">
/// The absolute path of the resource.
/// </param>
public record ImportContext(string Identifier, string Path) : IIdentifiable<string>
{
    /// <inheritdoc />
    public string Identifier { get; init; } = Identifier;
}

/// <summary>
/// Imports and caches resources.
/// </summary>
/// <remarks>
/// The importer owns and destroys its registered decoders and successfully imported resources.
/// Imported resources are borrowed by callers and must not be destroyed by them.
/// </remarks>
public class Importer : Module
{
    private readonly Dictionary<ResourceKey, Resource> _resources = [];
    private readonly HashSet<Resource> _pendingCleanup = [];
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the registered resource decoders.
    /// </summary>
    /// <remarks>
    /// The importer owns and destroys its registered decoders.
    /// Registration transfers ownership to this owner. Removing or clearing entries returns
    /// ownership to the caller without destroying them. Do not register an object owned elsewhere.
    /// </remarks>
    public readonly ReactiveSet<Decoder> Decoders;

    /// <summary>
    /// Gets the absolute root directory used to locate resources.
    /// </summary>
    public readonly string Root;

    /// <summary>
    /// Initializes a new instance of the <see cref="Importer"/> class.
    /// </summary>
    /// <param name="root">
    /// The directory from which resources are imported.
    /// </param>
    /// <param name="decoders">
    /// The initial decoders, or <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="root"/> is empty or whitespace.
    /// </exception>
    public Importer(string root, IEnumerable<Decoder>? decoders = null)
        : base("Importer", ["Logger"])
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
        Decoders = [];
        foreach (var decoder in decoders ?? [])
        {
            Decoders.Add(decoder);
        }
    }

    private SourcedLogger Logger => Require<Logger>("Logger").From(Identifier);

    private void ComposeContents()
    {
        if (InjectedDependencies is not null)
            Logger.Log("Composing module contents.", LogMessageKind.Debug);
        foreach (var decoder in Compose())
        {
            try
            {
                Decoders.Add(decoder);
            }
            catch
            {
                if (!Decoders.Contains(decoder) && !decoder.Destroyed)
                    decoder.Destroy();

                throw;
            }
        }
        if (InjectedDependencies is not null)
            Logger.Log("Composition completed.", LogMessageKind.Debug);
    }

    private void ConfigureContents()
    {
        if (InjectedDependencies is not null)
            Logger.Log("Configuring module.", LogMessageKind.Debug);
        Configure();
        if (InjectedDependencies is not null)
            Logger.Log("Configuration completed.", LogMessageKind.Debug);
    }

    private string ResolvePath(string path)
    {
        var absolutePath = Path.GetFullPath(path, Root);

        var relativePath = Path.GetRelativePath(Root, absolutePath);

        if (
            relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
        )
        {
            throw new ArgumentException(
                $"Resource path '{path}' is outside the importer root.",
                nameof(path)
            );
        }

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException(
                $"Resource '{relativePath}' was not found.",
                absolutePath
            );
        }

        return absolutePath;
    }

    /// <summary>
    /// Composes the decoders managed by this object.
    /// </summary>
    /// <returns>The decoders to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once when the importer first starts or first imports a resource.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// The importer owns and destroys its registered decoders.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<Decoder> Compose()
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

        foreach (var resource in _resources.Values.Where(resource => !resource.Destroyed))
        {
            resource.Destroy();
            if (InjectedDependencies is not null)
                Logger.Log(
                    $"Destroyed cached {resource.GetType().Name} resource.",
                    LogMessageKind.Debug
                );
        }

        _resources.Clear();

        foreach (var resource in _pendingCleanup.ToArray())
        {
            if (!resource.Destroyed)
                resource.Destroy();
        }
        _pendingCleanup.Clear();

        foreach (var decoder in Decoders.ToArray())
        {
            if (!decoder.Destroyed)
                decoder.Destroy();
        }

        if (!Decoders.Destroyed)
            Decoders.Destroy();
        if (InjectedDependencies is not null)
            Logger.Log("Module resources destroyed.");
    }

    /// <inheritdoc />
    protected override void OnStart()
    {
        Logger.Log("Starting module.");
        _preparator.Ensure(ComposeContents, ConfigureContents);
        Logger.Log("Module started.");
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        Logger.Log("Module stopped.");
        base.OnStop();
    }

    /// <summary>
    /// Imports or retrieves a cached resource.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type of resource to import.
    /// </typeparam>
    /// <param name="path">
    /// The resource path relative to <see cref="Root"/>.
    /// </param>
    /// <returns>
    /// The imported resource.
    /// </returns>
    /// <remarks>
    /// Resources are cached by requested type and normalized relative path.
    /// The importer owns the returned resource. A decoded resource is destroyed if
    /// it cannot be adopted by the cache. Failures after decoder selection retain their original type
    /// and include Mirage.ResourcePath, Mirage.ResourceType and Mirage.Decoder in Exception.Data.
    /// Cleanup failures are aggregated with the original import error. Resources whose
    /// cleanup failed remain owned by the importer and are retried during its destruction.
    /// </remarks>
    /// <exception cref="Mirage.Common.Lifecycle.DestroyedObjectException">
    /// Thrown when the importer has been destroyed.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or resolves outside <see cref="Root"/>.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the resource file does not exist.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the path has no extension or no decoder supports the requested resource type and extension.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple decoders match or a decoder returns the wrong resource type.
    /// </exception>
    /// <exception cref="AggregateException">Importing and releasing an unadopted resource both failed.</exception>
    public TResource Import<TResource>(string path)
        where TResource : Resource
    {
        ThrowIfDestroyed();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _preparator.Ensure(ComposeContents, ConfigureContents);

        if (InjectedDependencies is not null)
            Logger.Log(
                $"Importing resource '{path}' as {typeof(TResource).Name}.",
                LogMessageKind.Debug
            );
        var absolutePath = ResolvePath(path);

        var identifier = Path.GetRelativePath(Root, absolutePath);

        var key = new ResourceKey(typeof(TResource), identifier);

        if (_resources.TryGetValue(key, out var cached))
        {
            if (cached.Destroyed)
            {
                _resources.Remove(key);
                if (InjectedDependencies is not null)
                    Logger.Log(
                        $"Evicted destroyed resource '{identifier}' from cache.",
                        LogMessageKind.Debug
                    );
            }
            else
            {
                if (InjectedDependencies is not null)
                    Logger.Log($"Using cached resource '{identifier}'.", LogMessageKind.Debug);
                return (TResource)cached;
            }
        }

        var extension = Path.GetExtension(absolutePath);

        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new NotSupportedException($"Resource '{identifier}' has no filename extension.");
        }

        var candidates = Decoders
            .Where(decoder =>
                decoder.ResourceType == typeof(TResource) && decoder.Supports(extension)
            )
            .Take(2)
            .ToArray();

        switch (candidates.Length)
        {
            case 0:
                throw new NotSupportedException(
                    $"No decoder is registered for resource type "
                        + $"'{typeof(TResource).Name}' and extension "
                        + $"'{extension}'."
                );

            case > 1:
                throw new InvalidOperationException(
                    $"Multiple decoders are registered for resource type "
                        + $"'{typeof(TResource).Name}' and extension "
                        + $"'{extension}'."
                );
        }

        var decoder = candidates[0];
        Resource? decoded = null;
        try
        {
            using var stream = File.OpenRead(absolutePath);
            var context = new ImportContext(identifier, absolutePath);
            decoded = decoder.Decode(context, stream);

            if (decoded is not TResource resource)
                throw new InvalidOperationException(
                    $"Decoder '{decoder.GetType().Name}' returned "
                        + $"'{decoded?.GetType().Name ?? "null"}' instead of '{typeof(TResource).Name}'."
                );

            _resources.Add(key, resource);
            decoded = null; // The cache now owns the resource.
            if (InjectedDependencies is not null)
                Logger.Log($"Imported resource '{identifier}' as {typeof(TResource).Name}.");

            return resource;
        }
        catch (Exception exception)
        {
            exception.Data["Mirage.ResourcePath"] = absolutePath;
            exception.Data["Mirage.ResourceType"] = typeof(TResource).FullName;
            exception.Data["Mirage.Decoder"] = decoder.GetType().FullName;

            if (decoded is { Destroyed: false })
            {
                _pendingCleanup.Add(decoded);
                try
                {
                    decoded.Destroy();
                    _pendingCleanup.Remove(decoded);
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        $"Importing resource '{identifier}' and cleanup failed.",
                        exception,
                        cleanupFailure
                    );
                }
            }

            throw;
        }
    }

    private readonly record struct ResourceKey(Type Type, string Identifier);
}
