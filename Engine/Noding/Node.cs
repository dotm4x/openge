using Mirage.Common;
using Mirage.Common.Collections;
using Mirage.Common.Events;
using Mirage.Common.Interfaces;
using Mirage.Common.Lifecycle;
using Mirage.Common.Primitives;
using Mirage.Logging;

namespace Mirage.Noding;

internal sealed class NodeContext(ModuleContainer modules)
{
    public ModuleContainer Modules { get; } = modules;
}

/// <summary>
/// Provides optional values used to initialize a <see cref="Node"/>.
/// </summary>
public class NodeOptions
{
    /// <summary>
    /// Gets the initial parent node. The default is <see langword="null"/>, which creates a root.
    /// </summary>
    public Node? Parent { get; init; }

    /// <summary>
    /// Gets whether the node remains loaded when its parent is unloaded. The default is <see langword="false"/>.
    /// </summary>
    public bool Persistent { get; init; }

    /// <summary>
    /// Gets the initial subnodes to add. The default is <see langword="null"/>, which adds none.
    /// </summary>
    public IEnumerable<Node>? Subnodes { get; init; }

    /// <summary>
    /// Gets the initial tags to assign. The default is <see langword="null"/>, which adds none.
    /// </summary>
    public IEnumerable<string>? Tags { get; init; }
}

/// <summary>
/// Initializes a new instance of the <see cref="NodeReactiveSet"/> class.
/// </summary>
/// <param name="owner">The node that owns this collection.</param>
/// <remarks>
/// <para>
/// Represents a collection of <see cref="Node"/> instances belonging to a parent node.
/// </para>
/// In addition to the standard <see cref="ReactiveSet{TItem}"/> collection operations,
/// this class provides methods for locating nodes by identifier, name, path, or tag.
/// Searches can optionally include descendant nodes recursively.
/// </remarks>
public class NodeReactiveSet(Node owner) : ReactiveSet<Node>
{
    /// <summary>
    /// Gets a node by its unique identifier.
    /// </summary>
    /// <param name="identifier">The unique identifier of the node to locate.</param>
    /// <param name="recursive">
    /// Whether to search descendant nodes recursively.
    /// </param>
    /// <returns>
    /// The node with the specified identifier, or <see langword="null"/> if no
    /// matching node was found.
    /// </returns>
    public Node? GetByIdentifier(Guid identifier, bool recursive = false)
    {
        foreach (var node in this)
        {
            if (node.Identifier == identifier)
                return node;

            if (!recursive)
                continue;

            var result = node.Subnodes.GetByIdentifier(identifier, true);

            if (result is not null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Gets a node of the specified type by its unique identifier.
    /// </summary>
    /// <typeparam name="TNode">
    /// The type of node to locate.
    /// </typeparam>
    /// <param name="identifier">The unique identifier of the node to locate.</param>
    /// <param name="recursive">
    /// Whether to search descendant nodes recursively.
    /// </param>
    /// <returns>
    /// The matching node, or <see langword="null"/> if no node with the specified
    /// identifier and type was found.
    /// </returns>
    public TNode? GetByIdentifier<TNode>(Guid identifier, bool recursive = false)
        where TNode : Node
    {
        foreach (var node in this)
        {
            if (node.Identifier == identifier && node is TNode typedNode)
                return typedNode;

            if (!recursive)
                continue;

            var result = node.Subnodes.GetByIdentifier<TNode>(identifier, true);

            if (result is not null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Gets a node by its name.
    /// </summary>
    /// <param name="name">The name of the node to locate.</param>
    /// <param name="recursive">
    /// Whether to search descendant nodes recursively.
    /// </param>
    /// <returns>
    /// The first node with the specified name, or <see langword="null"/> if no
    /// matching node was found.
    /// </returns>
    public Node? GetByName(string name, bool recursive = false)
    {
        foreach (var node in this)
        {
            if (node.Name.Get() == name)
                return node;

            if (!recursive)
                continue;

            var result = node.Subnodes.GetByName(name, true);

            if (result is not null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Gets a node of the specified type by its name.
    /// </summary>
    /// <typeparam name="TNode">
    /// The type of node to locate.
    /// </typeparam>
    /// <param name="name">The name of the node to locate.</param>
    /// <param name="recursive">
    /// Whether to search descendant nodes recursively.
    /// </param>
    /// <returns>
    /// The first matching node, or <see langword="null"/> if no node with the
    /// specified name and type was found.
    /// </returns>
    public TNode? GetByName<TNode>(string name, bool recursive = false)
        where TNode : Node
    {
        foreach (var node in this)
        {
            if (node.Name.Get() == name && node is TNode typedNode)
                return typedNode;

            if (!recursive)
                continue;

            var result = node.Subnodes.GetByName<TNode>(name, true);

            if (result is not null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Gets a node using a path relative to the owner of this collection.
    /// </summary>
    /// <param name="path">
    /// The path to resolve. A path is composed of node names separated by
    /// <c>/</c>. The <c>.</c> segment refers to the current node and the
    /// <c>..</c> segment refers to its parent.
    /// </param>
    /// <returns>
    /// The node resolved by the path, or <see langword="null"/> if the path
    /// cannot be resolved.
    /// </returns>
    /// <remarks>
    /// Path resolution begins at the node that owns this collection.
    /// </remarks>
    public Node? GetByPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var current = owner;

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case ".":
                    break;

                case "..":
                    current = current.Parent.Get();
                    break;

                default:
                    current = current.Subnodes.GetByName(segment);
                    break;
            }

            if (current is null)
                return null;
        }

        return current;
    }

    /// <summary>
    /// Gets a node of the specified type using a path relative to the owner
    /// of this collection.
    /// </summary>
    /// <typeparam name="TNode">
    /// The type of node to locate.
    /// </typeparam>
    /// <param name="path">
    /// The path to resolve. A path is composed of node names separated by
    /// <c>/</c>.
    /// </param>
    /// <returns>
    /// The node resolved by the path if it is of the requested type;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    public TNode? GetByPath<TNode>(string path)
        where TNode : Node
    {
        return GetByPath(path) as TNode;
    }

    /// <summary>
    /// Gets all nodes containing the specified tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <param name="recursive">
    /// Whether to search descendant nodes recursively.
    /// </param>
    /// <returns>
    /// An enumerable sequence containing all matching nodes.
    /// </returns>
    /// <remarks>
    /// When <paramref name="recursive"/> is <see langword="false"/>, only direct
    /// subnodes are searched.
    /// </remarks>
    public IEnumerable<Node> GetByTag(string tag, bool recursive = false)
    {
        foreach (var node in this)
        {
            if (node.Tags.Contains(tag))
                yield return node;

            if (!recursive)
                continue;

            foreach (var result in node.Subnodes.GetByTag(tag, true))
                yield return result;
        }
    }

    /// <summary>
    /// Gets all nodes of the specified type containing the specified tag.
    /// </summary>
    /// <typeparam name="TNode">
    /// The type of node to locate.
    /// </typeparam>
    /// <param name="tag">The tag to search for.</param>
    /// <param name="recursive">
    /// Whether to search descendant nodes recursively.
    /// </param>
    /// <returns>
    /// An enumerable sequence containing all matching nodes of the requested type.
    /// </returns>
    /// <remarks>
    /// When <paramref name="recursive"/> is <see langword="false"/>, only direct
    /// subnodes are searched.
    /// </remarks>
    public IEnumerable<TNode> GetByTag<TNode>(string tag, bool recursive = false)
        where TNode : Node
    {
        foreach (var node in this)
        {
            if (node is TNode typedNode && node.Tags.Contains(tag))
                yield return typedNode;

            if (!recursive)
                continue;

            foreach (var result in node.Subnodes.GetByTag<TNode>(tag, true))
                yield return result;
        }
    }
}

/// <summary>
/// Represents a node in a hierarchical graph.
/// </summary>
/// <remarks>
/// A node can have a parent, contain subnodes, and participate in the graph
/// lifecycle through loading, unloading, and destruction.
///
/// Nodes are identified independently by a unique <see cref="Identifier"/> and
/// a mutable human-readable <see cref="Name"/>.
///
/// Nodes managed by a <see cref="NodeManager"/> receive access to the module
/// dependencies exposed by that manager. The same dependency context is
/// propagated to subnodes as they enter the hierarchy. These dependencies are borrowed.
/// A parent owns its subnodes. Detaching returns ownership to the caller; reparenting
/// transfers it to the new parent. A registered manager root must be removed before reparenting.
/// </remarks>
public class Node : Destroyable, IIdentifiable<Guid>
{
    private readonly Preparator _preparator = new();
    private NodeContext? _context;
    private bool _restoringParent;
    private HashSet<Node>? _loadRollbackPreserved;
    private bool _loadRollbackInitialized;

    /// <summary>
    /// Gets the mutable name of the node.
    /// </summary>
    public readonly Store<string> Name;

    /// <summary>
    /// Gets the parent node of this node.
    /// </summary>
    /// <remarks>
    /// The value is <see langword="null"/> when the node is a root node.
    /// Changes to this store are automatically synchronized with the parent's
    /// <see cref="Subnodes"/> collection.
    /// </remarks>
    public readonly Store<Node?> Parent;

    /// <summary>
    /// Gets whether the node remains loaded when its parent is unloaded.
    /// </summary>
    /// <remarks>
    /// Persistent nodes are loaded with their parent, but are not automatically
    /// unloaded with it. They can still be unloaded or destroyed explicitly.
    /// </remarks>
    public readonly bool Persistent;

    /// <summary>
    /// Gets the collection of nodes directly contained by this node.
    /// </summary>
    /// <remarks>Adding transfers ownership to this node. Removing or clearing detaches
    /// nodes without destroying them and returns ownership to the caller.</remarks>
    public readonly NodeReactiveSet Subnodes;

    /// <summary>
    /// Gets the tags assigned to this node.
    /// </summary>
    public readonly ReactiveSet<string> Tags = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Node"/> class.
    /// </summary>
    /// <param name="name">
    /// The initial name of the node.
    /// </param>
    /// <param name="options">
    /// The optional values used to initialize the node.
    /// </param>
    public Node(string name, NodeOptions? options = null)
    {
        Persistent = options?.Persistent ?? false;

        Name = new Store<string>(name);
        Parent = new Store<Node?>(null);
        Subnodes = new NodeReactiveSet(this);

        Parent.Connect(OnParentChanged, true);
        Subnodes.OnAdd.Connect(OnSubnodeAdded, true);
        Subnodes.OnRemove.Connect(OnSubnodeRemoved, true);

        if (options is null)
            return;

        foreach (var node in options.Subnodes ?? [])
            Subnodes.Add(node);

        if (options.Parent is not null)
            Parent.Set(options.Parent);

        foreach (var tag in options.Tags ?? [])
            Tags.Add(tag);
    }

    /// <summary>
    /// Gets a logger whose source identifies this node.
    /// </summary>
    protected SourcedLogger Logger => Require<Logger>("Logger").From(ToString());

    /// <summary>
    /// Gets a direct subnode by its name.
    /// </summary>
    /// <param name="name">The name of the subnode to get.</param>
    /// <returns>
    /// The subnode with the specified name, or <see langword="null"/> if no
    /// matching subnode was found.
    /// </returns>
    public Node? this[string name] => Subnodes.GetByName(name);

    /// <summary>
    /// Gets a value indicating whether the node is currently loaded.
    /// </summary>
    /// <remarks>
    /// A node must have a loaded parent before it can be loaded itself.
    /// </remarks>
    public bool Loaded { get; private set; }

    /// <summary>
    /// Gets the hierarchical path of the node.
    /// </summary>
    /// <remarks>
    /// The path is constructed from the names of the node and all of its
    /// ancestors. Root nodes use only their own name.
    /// </remarks>
    public string Path
    {
        get
        {
            var parent = Parent.Get();

            return parent is null ? Name.Get() : $"{parent.Path}/{Name.Get()}";
        }
    }

    /// <inheritdoc />
    public Guid Identifier { get; } = Guid.NewGuid();

    private void ComposeContents()
    {
        foreach (var node in Compose())
        {
            try
            {
                Subnodes.Add(node);
            }
            catch
            {
                if (node.Parent.Get() is null && node.RootOwner is null && !node.Destroyed)
                    node.Destroy();

                throw;
            }
        }
    }

    private void ConfigureContents()
    {
        Configure();
    }

    private void OnParentChanged(Node? parent)
    {
        if (_restoringParent)
            return;

        var previous = Parent.Previous;

        try
        {
            ValidateParent(parent);

            if (parent?._context is not null)
                Inject(parent._context);
        }
        catch
        {
            _restoringParent = true;

            try
            {
                Parent.Set(previous);
            }
            finally
            {
                _restoringParent = false;
            }

            throw;
        }

        if (previous is not null && previous.Subnodes.Contains(this))
            previous.Subnodes.Remove(this);

        if (parent is not null && !parent.Subnodes.Contains(this))
            parent.Subnodes.Add(this);

        var shouldBeLoaded = parent?.Loaded == true;

        switch (shouldBeLoaded)
        {
            case true when !Loaded:
                Load();
                break;

            case false when Loaded && !Persistent:
                Unload();
                break;
        }
    }

    private void OnSubnodeAdded(Node node)
    {
        try
        {
            node.ThrowIfDestroyed();
            if (_context is not null)
                node.Inject(_context);

            if (node.Parent.Get() != this)
                node.Parent.Set(this);
        }
        catch
        {
            if (Subnodes.Contains(node))
                Subnodes.Remove(node);

            throw;
        }
    }

    private void OnSubnodeRemoved(Node node)
    {
        if (node.Parent.Get() == this)
            node.Parent.Set(null);
    }

    private void ValidateParent(Node? parent)
    {
        if (parent is null)
            return;

        parent.ThrowIfDestroyed();

        if (RootOwner is not null)
            throw new InvalidOperationException(
                "A registered root must be removed from its manager before parenting."
            );

        if (ReferenceEquals(parent, this))
            throw new InvalidOperationException($"{this} cannot be its own parent.");

        HashSet<Node> visited = [];

        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent.Get())
        {
            if (ReferenceEquals(ancestor, this))
            {
                throw new InvalidOperationException(
                    $"Cannot set {parent} as the parent of {this} because it would create a cycle."
                );
            }

            if (!visited.Add(ancestor))
            {
                throw new InvalidOperationException(
                    $"Cannot assign {parent} because its hierarchy already contains a cycle."
                );
            }
        }
    }

    /// <summary>
    /// Composes the subnodes managed by this object.
    /// </summary>
    /// <returns>The subnodes to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once before the first load, before this node or its subnodes start loading.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// Composed subnodes inherit this node's injected module context when available.
    /// The node owns and destroys its subnodes.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<Node> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition, before startup or first use.
    /// </summary>
    /// <remarks>
    /// All constructor-provided and composed objects are available here.
    /// Injected module dependencies are available through <see cref="Require{TModule}()"/>
    /// when the node is managed by a node manager.
    /// This hook is invoked at most once, including across later lifecycle cycles.
    /// If configuration throws, later lifecycle calls reject further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        if (Loaded)
            Unload();

        Parent.Set(null);
        foreach (var node in Subnodes.ToArray())
            node.Destroy();

        Name.Destroy();
        Subnodes.Destroy();
        Tags.Destroy();
        Parent.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Called when the node is loaded.
    /// </summary>
    /// <remarks>
    /// Override this method to perform node-specific initialization that should
    /// occur when the node enters the loaded state.
    /// </remarks>
    protected virtual void OnLoad() { }

    /// <summary>
    /// Called when the node is unloaded.
    /// </summary>
    /// <remarks>
    /// Override this method to perform node-specific cleanup that should occur
    /// when the node leaves the loaded state.
    /// </remarks>
    protected virtual void OnUnload() { }

    /// <summary>
    /// Gets an injected module dependency of the node manager that owns this node.
    /// </summary>
    /// <typeparam name="TModule">The type of module to retrieve.</typeparam>
    /// <returns>The borrowed injected module, which is owned by the engine.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the node has not been injected or no single dependency
    /// matches the specified type.
    /// </exception>
    protected TModule Require<TModule>()
        where TModule : Module
    {
        ThrowIfDestroyed();

        if (_context is null)
            throw new InvalidOperationException($"{this} has not been injected.");

        return _context.Modules.Get<TModule>();
    }

    /// <summary>
    /// Gets an injected module dependency of the node manager that owns this node.
    /// </summary>
    /// <typeparam name="TModule">The expected type of the module.</typeparam>
    /// <param name="identifier">The identifier of the module to retrieve.</param>
    /// <returns>The borrowed injected module, which is owned by the engine.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the node has not been injected, the dependency is unavailable,
    /// or the dependency is not of the requested type.
    /// </exception>
    protected TModule Require<TModule>(string identifier)
        where TModule : Module
    {
        ThrowIfDestroyed();

        if (_context is null)
            throw new InvalidOperationException($"{this} has not been injected.");

        var module = _context.Modules.Get(identifier);

        if (module is not TModule typedModule)
        {
            throw new InvalidOperationException(
                $"Node '{Path}' requires dependency '{identifier}' to be of type '{typeof(TModule).Name}', but it is '{module.GetType().Name}'."
            );
        }

        return typedModule;
    }

    /// <summary>
    /// Throws an exception if the node is not currently loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the node is not loaded.
    /// </exception>
    protected void ThrowIfNotLoaded()
    {
        if (Loaded)
            return;

        throw new InvalidOperationException($"{this} is not loaded.");
    }

    /// <summary>
    /// Loads the node and its subnodes that are not already loaded.
    /// </summary>
    /// <remarks>
    /// The node must not already be loaded. If it has a parent, that parent
    /// must be loaded first.
    ///
    /// Composition and configuration occur before the node enters the loaded state.
    /// When the node belongs to a node manager, its module dependencies have already
    /// been injected before configuration occurs.
    ///
    /// <see cref="OnLoad"/> is invoked after this node enters the loaded state
    /// and before its subnodes are loaded. Persistent subnodes that remained
    /// loaded are not loaded again.
    /// If loading a subnode fails, newly loaded subnodes are unloaded in reverse order,
    /// including persistent ones, and completed initialization is undone through OnUnload.
    /// Previously loaded subnodes are preserved. If rollback fails, this node remains
    /// loaded so Unload can retry cleanup. An incomplete OnLoad must clean up its own resources.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the node has already been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the node is already loaded or its parent is not loaded.
    /// </exception>
    public void Load()
    {
        ThrowIfDestroyed();

        if (Loaded)
            throw new InvalidOperationException($"{this} is already loaded");

        var parent = Parent.Get();

        if (parent is not null && !parent.Loaded)
        {
            throw new InvalidOperationException(
                $"{this} cannot be loaded because its parent is not loaded."
            );
        }

        _preparator.Ensure(ComposeContents, ConfigureContents);

        Loaded = true;
        var initialized = false;
        var previouslyLoaded = Subnodes.Where(node => node.Loaded).ToHashSet();

        try
        {
            OnLoad();
            initialized = true;

            foreach (var node in Subnodes)
            {
                if (!node.Loaded)
                    node.Load();
            }
        }
        catch (Exception exception)
        {
            List<Exception> failures = [];
            foreach (var node in Subnodes.ToArray().Reverse())
            {
                if (!node.Loaded || previouslyLoaded.Contains(node))
                    continue;

                try
                {
                    node.Unload();
                }
                catch (Exception cleanupFailure)
                {
                    failures.Add(cleanupFailure);
                    break;
                }
            }

            if (failures.Count == 0)
            {
                try
                {
                    if (initialized)
                        OnUnload();
                    Loaded = false;
                }
                catch (Exception cleanupFailure)
                {
                    failures.Add(cleanupFailure);
                }
            }

            if (failures.Count != 0)
            {
                _loadRollbackPreserved = previouslyLoaded;
                _loadRollbackInitialized = initialized;
                throw new AggregateException(
                    "Node loading and rollback failed. Unload to retry cleanup.",
                    [exception, .. failures]
                );
            }

            throw;
        }
    }

    /// <summary>
    /// Returns a string representation of the node.
    /// </summary>
    /// <returns>
    /// A string containing the node's path and abbreviated unique identifier.
    /// </returns>
    public override string ToString()
    {
        return $"Node \"{Path}\" ({Identifier.ToString()[..8]})";
    }

    /// <summary>
    /// Unloads the node and its loaded, non-persistent subnodes.
    /// </summary>
    /// <remarks>
    /// Persistent subnodes retain their current loaded state. Non-persistent
    /// subnodes are unloaded before their parent. <see cref="OnUnload"/> is
    /// invoked before this node leaves the loaded state.
    /// After a failed load rollback, this also retries cleanup of newly loaded persistent
    /// subnodes while preserving subnodes that were already loaded before the attempt.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the node has already been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the node is not currently loaded.
    /// </exception>
    public void Unload()
    {
        ThrowIfDestroyed();

        if (!Loaded)
            throw new InvalidOperationException($"{this} is not loaded");

        var preserved = _loadRollbackPreserved;
        var rollingBack = preserved is not null;
        var nodes = rollingBack ? Subnodes.ToArray().Reverse() : Subnodes;
        foreach (var node in nodes)
        {
            if (
                node.Loaded
                && (preserved is not null ? !preserved.Contains(node) : !node.Persistent)
            )
                node.Unload();
        }

        if (!rollingBack || _loadRollbackInitialized)
            OnUnload();
        Loaded = false;
        _loadRollbackPreserved = null;
        _loadRollbackInitialized = false;
    }

    internal NodeManager? RootOwner { get; set; }

    internal void Inject(NodeContext context)
    {
        ThrowIfDestroyed();

        if (_context is not null)
        {
            if (ReferenceEquals(_context, context))
                return;

            throw new InvalidOperationException(
                $"{this} has already been injected with a different node context."
            );
        }

        _context = context;

        _preparator.EnsureComposed(ComposeContents);

        foreach (var node in Subnodes)
            node.Inject(context);
    }
}
