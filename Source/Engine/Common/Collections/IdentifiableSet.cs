using System.Collections;
using OpenGE.Common.Events;
using OpenGE.Common.Interfaces;
using OpenGE.Common.Lifecycle;
using OpenGE.Common.Primitives;

namespace OpenGE.Common.Collections;

/// <summary>
/// Provides read-only access to a set of uniquely identifiable items.
/// </summary>
/// <typeparam name="TIdentifier">
/// The type used to identify items in the set.
/// </typeparam>
/// <typeparam name="TItem">
/// The type of items stored in the set.
/// </typeparam>
public interface IReadOnlyIdentifiableSet<TIdentifier, TItem> : IEnumerable<TItem>
    where TIdentifier : notnull
    where TItem : IIdentifiable<TIdentifier>
{
    /// <summary>
    /// Gets the number of items currently contained in the set.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the item associated with the specified identifier.
    /// </summary>
    /// <param name="identifier">The identifier of the item to get.</param>
    /// <returns>The item associated with the specified identifier.</returns>
    TItem this[TIdentifier identifier] { get; }

    /// <summary>
    /// Determines whether an item with the specified identifier is contained in the set.
    /// </summary>
    /// <param name="identifier">The identifier to locate.</param>
    /// <returns>
    /// <see langword="true"/> if an item with the specified identifier is contained
    /// in the set; otherwise, <see langword="false"/>.
    /// </returns>
    bool Contains(TIdentifier identifier);

    /// <summary>
    /// Determines whether the specified item is contained in the set.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the specified item is contained in the set;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool Contains(TItem item);

    /// <summary>
    /// Performs the specified action on each item in the set.
    /// </summary>
    /// <param name="action">The action to perform for each item.</param>
    void ForEach(Action<TItem> action);

    /// <summary>
    /// Creates an array containing all items in the set.
    /// </summary>
    /// <returns>A new array containing the items in the set.</returns>
    TItem[] ToArray();

    /// <summary>
    /// Creates a list containing all items in the set.
    /// </summary>
    /// <returns>A new list containing the items in the set.</returns>
    List<TItem> ToList();

    /// <summary>
    /// Attempts to retrieve the item associated with the specified identifier.
    /// </summary>
    /// <param name="identifier">The identifier to locate.</param>
    /// <param name="item">
    /// When this method returns, contains the item associated with the specified
    /// identifier, if the identifier was found; otherwise, the default value for
    /// <typeparamref name="TItem"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the identifier was found; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    bool TryGetValue(TIdentifier identifier, out TItem item);
}

/// <summary>
/// Represents a mutable collection of uniquely identifiable items, providing
/// reactive events and an optional capacity limit with automatic truncation.
/// </summary>
/// <remarks>
/// Items are indexed by their <see cref="IIdentifiable{TIdentifier}.Identifier"/>.
/// Two items with the same identifier cannot be contained in the set at the same time.
///
/// When the limit is reached, adding an item removes the oldest item first.
/// </remarks>
/// <typeparam name="TIdentifier">
/// The type used to identify items in the set.
/// </typeparam>
/// <typeparam name="TItem">
/// The type of items stored in the set.
/// </typeparam>
/// <remarks>
/// Items are borrowed. Removing, clearing, or destroying the set does not destroy them.
/// Destruction disconnects events and clears references without firing mutation events.
/// </remarks>
public class IdentifiableSet<TIdentifier, TItem>
    : Destroyable,
        IReadOnlyIdentifiableSet<TIdentifier, TItem>
    where TIdentifier : notnull
    where TItem : IIdentifiable<TIdentifier>
{
    private readonly Dictionary<TIdentifier, TItem> _items = [];
    private readonly Signal<TItem> _onAdd = new();
    private readonly Signal<Unit> _onClear = new();
    private readonly Signal<TItem> _onRemove = new();

    /// <summary>
    /// Gets the maximum number of items allowed in the identifiable set. A value of
    /// <c>0</c> indicates unlimited capacity.
    /// </summary>
    public readonly int Limit;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="IdentifiableSet{TIdentifier, TItem}"/> class.
    /// </summary>
    /// <param name="items">
    /// The initial items to add to the identifiable set, or <see langword="null"/>
    /// to start empty.
    /// </param>
    /// <param name="limit">
    /// The maximum number of items allowed in the identifiable set.
    /// A value of <c>0</c> means unlimited capacity.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="limit"/> is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initial items contain duplicate identifiers.
    /// </exception>
    public IdentifiableSet(IEnumerable<TItem>? items = null, int limit = 0)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit cannot be negative");

        Limit = limit;

        Add((items ?? []).ToArray());

        OnAdd = _onAdd;
        OnRemove = _onRemove;
        OnClear = _onClear;
    }

    /// <summary>
    /// Gets the event fired after an item is added to the identifiable set.
    /// </summary>
    public IReadOnlyEvent<TItem> OnAdd { get; }

    /// <summary>
    /// Gets the event fired before a clear operation removes its items.
    /// </summary>
    public IReadOnlyEvent<Unit> OnClear { get; }

    /// <summary>
    /// Gets the event fired after an item is removed from the identifiable set.
    /// </summary>
    public IReadOnlyEvent<TItem> OnRemove { get; }

    /// <summary>
    /// Determines whether an item with the specified identifier is contained in the set.
    /// </summary>
    /// <param name="identifier">The identifier to locate.</param>
    /// <returns>
    /// <see langword="true"/> if an item with the specified identifier is contained
    /// in the set; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Contains(TIdentifier identifier)
    {
        return _items.ContainsKey(identifier);
    }

    /// <summary>
    /// Determines whether the specified item is contained in the set.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the specified item is contained in the set;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool Contains(TItem item)
    {
        return _items.TryGetValue(item.Identifier, out var existing)
            && EqualityComparer<TItem>.Default.Equals(existing, item);
    }

    /// <summary>
    /// Gets the number of items currently contained in the identifiable set.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Performs the specified action on each item in the identifiable set.
    /// </summary>
    /// <param name="action">The action to perform for each item.</param>
    public void ForEach(Action<TItem> action)
    {
        foreach (var item in _items.Values)
            action(item);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the items in the identifiable set.
    /// </summary>
    /// <returns>An enumerator for the identifiable set.</returns>
    public IEnumerator<TItem> GetEnumerator()
    {
        return _items.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Gets the item associated with the specified identifier.
    /// </summary>
    /// <param name="identifier">The identifier of the item to get.</param>
    /// <returns>The item associated with the specified identifier.</returns>
    public TItem this[TIdentifier identifier] => _items[identifier];

    /// <summary>
    /// Creates an array containing all items in the identifiable set.
    /// </summary>
    /// <returns>A new array containing the items in the identifiable set.</returns>
    public TItem[] ToArray()
    {
        return [.. _items.Values];
    }

    /// <summary>
    /// Creates a list containing all items in the identifiable set.
    /// </summary>
    /// <returns>A new list containing the items in the identifiable set.</returns>
    public List<TItem> ToList()
    {
        return [.. _items.Values];
    }

    /// <summary>
    /// Attempts to retrieve the item associated with the specified identifier.
    /// </summary>
    /// <param name="identifier">The identifier to locate.</param>
    /// <param name="item">The item associated with the specified identifier.</param>
    /// <returns>
    /// <see langword="true"/> if the identifier was found; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(TIdentifier identifier, out TItem item)
    {
        return _items.TryGetValue(identifier, out item!);
    }

    private void Truncate()
    {
        if (Limit <= 0 || _items.Count < Limit)
            return;

        var identifier = _items.First().Key;

        Remove(identifier);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _onAdd.Destroy();
        _onRemove.Destroy();
        _onClear.Destroy();
        _items.Clear();

        base.OnDestroy();
    }

    /// <summary>
    /// Adds one or more items to the identifiable set.
    /// </summary>
    /// <remarks>
    /// Duplicate identifiers in the batch or the current set are rejected before
    /// any items are added or events are fired. Event callbacks can mutate the set;
    /// failures caused by those callbacks do not roll back earlier additions.
    /// </remarks>
    /// <param name="items">The items to add.</param>
    /// <returns>The items that were added.</returns>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the identifiable set has been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an identifier is repeated in the batch or already contained in the set.
    /// </exception>
    public TItem[] Add(params TItem[] items)
    {
        ThrowIfDestroyed();

        HashSet<TIdentifier> identifiers = [];
        foreach (var item in items)
        {
            if (_items.ContainsKey(item.Identifier) || !identifiers.Add(item.Identifier))
                throw new InvalidOperationException(
                    $"Duplicate item identifier: '{item.Identifier}'"
                );
        }

        foreach (var item in items)
        {
            if (_items.ContainsKey(item.Identifier))
            {
                throw new InvalidOperationException(
                    $"An item with the identifier '{item.Identifier}' is already in the identifiable set"
                );
            }

            Truncate();

            _items.Add(item.Identifier, item);
            _onAdd.Fire(item);
        }

        return items;
    }

    /// <summary>
    /// Removes all items from the identifiable set.
    /// </summary>
    /// <remarks>
    /// Fires <see cref="OnClear"/> before firing <see cref="OnRemove"/> for each item.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the identifiable set has been destroyed.
    /// </exception>
    public void Clear()
    {
        ThrowIfDestroyed();

        _onClear.Fire(Unit.Value);

        foreach (var identifier in _items.Keys.ToArray())
            Remove(identifier);
    }

    /// <summary>
    /// Removes the item with the specified identifier from the identifiable set.
    /// </summary>
    /// <param name="identifier">The identifier of the item to remove.</param>
    /// <returns>
    /// <see langword="true"/> if the item was successfully found and removed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the identifiable set has been destroyed.
    /// </exception>
    public bool Remove(TIdentifier identifier)
    {
        ThrowIfDestroyed();

        if (!_items.Remove(identifier, out var item))
            return false;

        _onRemove.Fire(item);

        return true;
    }

    /// <summary>
    /// Removes the specified item from the identifiable set.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the identifiable set has been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified item is not contained in the identifiable set.
    /// </exception>
    public void Remove(TItem item)
    {
        ThrowIfDestroyed();

        if (
            !_items.TryGetValue(item.Identifier, out var existing)
            || !EqualityComparer<TItem>.Default.Equals(existing, item)
        )
        {
            throw new InvalidOperationException(
                "Item is not in the identifiable set, cannot remove"
            );
        }

        _items.Remove(item.Identifier);
        _onRemove.Fire(existing);
    }
}
