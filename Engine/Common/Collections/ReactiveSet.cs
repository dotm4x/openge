using System.Collections;
using OpenGE.Common.Events;
using OpenGE.Common.Lifecycle;
using OpenGE.Common.Primitives;

namespace OpenGE.Common.Collections;

/// <summary>
/// Provides read-only access to the items in a reactive set.
/// </summary>
/// <typeparam name="TItem">The type of items stored in the reactive set.</typeparam>
public interface IReadOnlyReactiveSet<TItem> : IEnumerable<TItem>
{
    /// <summary>
    /// Gets the number of items currently contained in the reactive set.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Determines whether the specified item is contained in the reactive set.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the item is contained in the reactive set; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    bool Contains(TItem item);

    /// <summary>
    /// Performs the specified action on each item in the reactive set.
    /// </summary>
    /// <param name="action">The action to perform for each item.</param>
    void ForEach(Action<TItem> action);

    /// <summary>
    /// Creates an array containing all items in the reactive set.
    /// </summary>
    /// <returns>A new array containing the items in the reactive set.</returns>
    TItem[] ToArray();

    /// <summary>
    /// Creates a list containing all items in the reactive set.
    /// </summary>
    /// <returns>A new list containing the items in the reactive set.</returns>
    List<TItem> ToList();
}

/// <summary>
/// Represents a mutable collection of unique items, providing reactive events
/// and an optional capacity limit with automatic truncation.
/// </summary>
/// <remarks>
/// When the limit is reached, adding an item removes the oldest item first.
/// Items are borrowed. Removing, clearing, or destroying the collection does not destroy them.
/// Destruction disconnects its events and clears references without firing mutation events.
/// </remarks>
/// <typeparam name="TItem">The type of items stored in the reactive set.</typeparam>
public class ReactiveSet<TItem> : Destroyable, IReadOnlyReactiveSet<TItem>
{
    private readonly List<TItem> _items = [];
    private readonly Signal<TItem> _onAdd = new();
    private readonly Signal<Unit> _onClear = new();
    private readonly Signal<TItem> _onRemove = new();

    /// <summary>
    /// Gets the maximum number of items allowed in the reactive set. A value of
    /// <c>0</c> indicates unlimited capacity.
    /// </summary>
    public readonly int Limit;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveSet{TItem}"/> class.
    /// </summary>
    /// <param name="items">
    /// The initial items to add to the reactive set, or <see langword="null"/> to
    /// start empty.
    /// </param>
    /// <param name="limit">
    /// The maximum number of items allowed in the reactive set.
    /// A value of <c>0</c> means unlimited capacity.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="limit"/> is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initial items contain duplicates.
    /// </exception>
    public ReactiveSet(IEnumerable<TItem>? items = null, int limit = 0)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit cannot be negative");

        Limit = limit;

        foreach (var item in items ?? [])
            Add(item);

        OnAdd = _onAdd;
        OnRemove = _onRemove;
        OnClear = _onClear;
    }

    /// <summary>
    /// Gets the event fired after an item is added to the reactive set.
    /// </summary>
    public IReadOnlyEvent<TItem> OnAdd { get; }

    /// <summary>
    /// Gets the event fired before a clear operation removes its items.
    /// </summary>
    public IReadOnlyEvent<Unit> OnClear { get; }

    /// <summary>
    /// Gets the event fired before an item is removed from the reactive set.
    /// </summary>
    public IReadOnlyEvent<TItem> OnRemove { get; }

    /// <summary>
    /// Determines whether the specified item is contained in the reactive set.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the item is contained in the reactive set; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool Contains(TItem item)
    {
        return _items.Contains(item);
    }

    /// <summary>
    /// Gets the number of items currently contained in the reactive set.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Performs the specified action on each item in the reactive set.
    /// </summary>
    /// <param name="action">The action to perform for each item.</param>
    public void ForEach(Action<TItem> action)
    {
        foreach (var item in _items)
            action(item);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the items in the reactive set.
    /// </summary>
    /// <returns>An enumerator for the reactive set.</returns>
    public IEnumerator<TItem> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Creates an array containing all items in the reactive set.
    /// </summary>
    /// <returns>A new array containing the items in the reactive set.</returns>
    public TItem[] ToArray()
    {
        return [.. _items];
    }

    /// <summary>
    /// Creates a list containing all items in the reactive set.
    /// </summary>
    /// <returns>A new list containing the items in the reactive set.</returns>
    public List<TItem> ToList()
    {
        return [.. _items];
    }

    private void Truncate()
    {
        if (Limit > 0 && _items.Count >= Limit)
            Remove(_items[0]);
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
    /// Adds one or more items to the reactive set.
    /// </summary>
    /// <param name="items">The items to add.</param>
    /// <returns>The items that were added.</returns>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the reactive set has been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an item is already contained in the reactive set.
    /// </exception>
    public TItem[] Add(params TItem[] items)
    {
        ThrowIfDestroyed();

        foreach (var item in items)
        {
            if (_items.Contains(item))
                throw new InvalidOperationException(
                    "Item is already in the reactive set, cannot add again"
                );

            Truncate();
            _items.Add(item);
            _onAdd.Fire(item);
        }

        return items;
    }

    /// <summary>
    /// Removes all items from the reactive set.
    /// </summary>
    /// <remarks>Fires <see cref="OnClear"/> before firing <see cref="OnRemove"/> for each item.</remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the reactive set has been destroyed.
    /// </exception>
    public void Clear()
    {
        ThrowIfDestroyed();

        _onClear.Fire(Unit.Value);

        foreach (var item in _items.ToArray())
            Remove(item);
    }

    /// <summary>
    /// Removes one or more items from the reactive set.
    /// </summary>
    /// <param name="items">The items to remove.</param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the reactive set has been destroyed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an item is not contained in the reactive set.
    /// </exception>
    public void Remove(params TItem[] items)
    {
        ThrowIfDestroyed();

        foreach (var item in items)
        {
            if (!_items.Contains(item))
                throw new InvalidOperationException(
                    "Item is not in the reactive set, cannot remove"
                );

            _onRemove.Fire(item);
            _items.Remove(item);
        }
    }
}
