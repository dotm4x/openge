using System.Collections;
using OpenGE.Common.Events;
using OpenGE.Common.Lifecycle;
using OpenGE.Common.Primitives;

namespace OpenGE.Common.Collections;

/// <summary>
/// Provides read-only access to the entries in a reactive dictionary.
/// </summary>
public interface IReadOnlyReactiveDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    /// <summary>
    /// Gets the number of entries currently contained in the reactive dictionary.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get.</param>
    /// <returns>The value associated with the specified key.</returns>
    TValue this[TKey key] { get; }

    /// <summary>
    /// Determines whether the specified key is contained in the reactive dictionary.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the key is contained in the reactive dictionary;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool ContainsKey(TKey key);

    /// <summary>
    /// Determines whether the specified value is contained in the reactive dictionary.
    /// </summary>
    /// <param name="value">The value to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the value is contained in the reactive dictionary;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool ContainsValue(TValue value);

    /// <summary>
    /// Performs the specified action on each entry in the reactive dictionary.
    /// </summary>
    /// <param name="action">The action to perform for each entry.</param>
    void ForEach(Action<TKey, TValue> action);

    /// <summary>
    /// Creates an array containing all entries in the reactive dictionary.
    /// </summary>
    /// <returns>A new array containing the entries in the reactive dictionary.</returns>
    KeyValuePair<TKey, TValue>[] ToArray();

    /// <summary>
    /// Creates a list containing all entries in the reactive dictionary.
    /// </summary>
    /// <returns>A new list containing the entries in the reactive dictionary.</returns>
    List<KeyValuePair<TKey, TValue>> ToList();

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified key,
    /// if the key was found; otherwise, the default value for <typeparamref name="TValue"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the key was found; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGetValue(TKey key, out TValue value);
}

/// <summary>
/// Represents a mutable collection of key-value pairs, providing reactive events.
/// </summary>
/// <remarks>
/// Values are borrowed. Removing, replacing, clearing, or destroying the dictionary does not
/// destroy them. Destruction disconnects events and clears references without mutation events.
/// </remarks>
/// <typeparam name="TKey">The type of keys stored in the reactive dictionary.</typeparam>
/// <typeparam name="TValue">The type of values stored in the reactive dictionary.</typeparam>
public class ReactiveDictionary<TKey, TValue>
    : Destroyable,
        IReadOnlyReactiveDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items = [];
    private readonly Signal<KeyValuePair<TKey, TValue>> _onAdd = new();
    private readonly Signal<Unit> _onClear = new();
    private readonly Signal<KeyValuePair<TKey, TValue>> _onRemove = new();

    private readonly Signal<(
        KeyValuePair<TKey, TValue> Previous,
        KeyValuePair<TKey, TValue> Current
    )> _onUpdate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveDictionary{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="items">
    /// The initial entries to add to the reactive dictionary, or <see langword="null"/>
    /// to start empty.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the initial entries contain duplicate keys.
    /// </exception>
    public ReactiveDictionary(IEnumerable<KeyValuePair<TKey, TValue>>? items = null)
    {
        foreach (var item in items ?? [])
            Add(item.Key, item.Value);

        OnAdd = _onAdd;
        OnRemove = _onRemove;
        OnUpdate = _onUpdate;
        OnClear = _onClear;
    }

    /// <summary>
    /// Gets the event fired after an entry is added to the reactive dictionary.
    /// </summary>
    public IReadOnlyEvent<KeyValuePair<TKey, TValue>> OnAdd { get; }

    /// <summary>
    /// Gets the event fired before a clear operation removes its entries.
    /// </summary>
    public IReadOnlyEvent<Unit> OnClear { get; }

    /// <summary>
    /// Gets the event fired after an entry is removed from the reactive dictionary.
    /// </summary>
    public IReadOnlyEvent<KeyValuePair<TKey, TValue>> OnRemove { get; }

    /// <summary>
    /// Gets the event fired after the value associated with an existing key is updated.
    /// </summary>
    public IReadOnlyEvent<(
        KeyValuePair<TKey, TValue> Previous,
        KeyValuePair<TKey, TValue> Current
    )> OnUpdate { get; }

    /// <summary>
    /// Gets the values currently contained in the reactive dictionary.
    /// </summary>
    public IEnumerable<TValue> Values => _items.Values;

    /// <summary>
    /// Determines whether the specified key is contained in the reactive dictionary.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the key is contained in the reactive dictionary;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool ContainsKey(TKey key)
    {
        return _items.ContainsKey(key);
    }

    /// <summary>
    /// Determines whether the specified value is contained in the reactive dictionary.
    /// </summary>
    /// <param name="value">The value to locate.</param>
    /// <returns>
    /// <see langword="true"/> if the value is contained in the reactive dictionary;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool ContainsValue(TValue value)
    {
        return _items.ContainsValue(value);
    }

    /// <summary>
    /// Gets the number of entries currently contained in the reactive dictionary.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Performs the specified action on each entry in the reactive dictionary.
    /// </summary>
    /// <param name="action">The action to perform for each entry.</param>
    public void ForEach(Action<TKey, TValue> action)
    {
        foreach (var item in _items)
            action(item.Key, item.Value);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the entries in the reactive dictionary.
    /// </summary>
    /// <returns>An enumerator for the reactive dictionary.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get => _items[key];
        set
        {
            ThrowIfDestroyed();

            var current = new KeyValuePair<TKey, TValue>(key, value);

            if (!_items.TryGetValue(key, out var previousValue))
            {
                _items[key] = value;
                _onAdd.Fire(current);
                return;
            }

            var previous = new KeyValuePair<TKey, TValue>(key, previousValue);

            _items[key] = value;
            _onUpdate.Fire((previous, current));
        }
    }

    /// <summary>
    /// Creates an array containing all entries in the reactive dictionary.
    /// </summary>
    /// <returns>A new array containing the entries in the reactive dictionary.</returns>
    public KeyValuePair<TKey, TValue>[] ToArray()
    {
        return [.. _items];
    }

    /// <summary>
    /// Creates a list containing all entries in the reactive dictionary.
    /// </summary>
    /// <returns>A new list containing the entries in the reactive dictionary.</returns>
    public List<KeyValuePair<TKey, TValue>> ToList()
    {
        return [.. _items];
    }

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <param name="value">The value associated with the specified key.</param>
    /// <returns>
    /// <see langword="true"/> if the key was found; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        return _items.TryGetValue(key, out value!);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _onAdd.Destroy();
        _onRemove.Destroy();
        _onUpdate.Destroy();
        _onClear.Destroy();
        _items.Clear();

        base.OnDestroy();
    }

    /// <summary>
    /// Adds the specified key and value to the reactive dictionary.
    /// </summary>
    /// <param name="key">The key of the entry to add.</param>
    /// <param name="value">The value of the entry to add.</param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the dictionary has been destroyed.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the key is already present.
    /// </exception>
    public void Add(TKey key, TValue value)
    {
        ThrowIfDestroyed();

        _items.Add(key, value);

        _onAdd.Fire(new KeyValuePair<TKey, TValue>(key, value));
    }

    /// <summary>
    /// Removes all entries from the reactive dictionary.
    /// </summary>
    /// <remarks>Fires <see cref="OnClear"/> before firing <see cref="OnRemove"/> for each entry.</remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the dictionary has been destroyed.
    /// </exception>
    public void Clear()
    {
        ThrowIfDestroyed();

        _onClear.Fire(Unit.Value);

        foreach (var item in _items.ToArray())
            Remove(item.Key);
    }

    /// <summary>
    /// Removes the value with the specified key from the reactive dictionary.
    /// </summary>
    /// <param name="key">The key of the entry to remove.</param>
    /// <returns>
    /// <see langword="true"/> if the entry was successfully found and removed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the dictionary has been destroyed.
    /// </exception>
    public bool Remove(TKey key)
    {
        ThrowIfDestroyed();

        if (!_items.Remove(key, out var value))
            return false;

        _onRemove.Fire(new KeyValuePair<TKey, TValue>(key, value));

        return true;
    }
}
