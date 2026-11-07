using OpenGE.Common.Lifecycle;

namespace OpenGE.Common.Events;

/// <summary>
/// Provides read-only access to a store.
/// </summary>
/// <typeparam name="TValue">The type of the stored value.</typeparam>
public interface IReadOnlyStore<TValue> : IReadOnlyEvent<TValue>
{
    /// <summary>
    /// Gets the value held by the store before the most recent change.
    /// </summary>
    TValue Previous { get; }

    /// <summary>
    /// Gets the current value of the store.
    /// </summary>
    TValue Value { get; }
}

/// <summary>
/// Provides full access to a store, including value mutation.
/// </summary>
/// <typeparam name="TValue">The type of the stored value.</typeparam>
public interface IStore<TValue> : IReadOnlyStore<TValue>, IEvent<TValue>
{
    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    /// <remarks>
    /// Assigning notifies listeners only when the new value differs from the current value.
    /// </remarks>
    new TValue Value { get; set; }
}

/// <summary>
/// Represents a reactive state container that holds a value and notifies listeners
/// when the value changes.
/// </summary>
/// <typeparam name="TValue">The type of value stored in the container.</typeparam>
/// <remarks>
/// The current and previous values are borrowed; changing or destroying the store
/// never destroys them.
/// </remarks>
/// <example>
/// <code>
/// var store = new Store&lt;int&gt;(0);
///
/// store.Connect(value =>
/// {
///     Console.WriteLine($"Changed from {store.Previous} to {value}.");
/// });
///
/// store.Value = 5; // Logs: Changed from 0 to 5.
/// </code>
/// </example>
/// <param name="value">The initial value of the store.</param>
/// <param name="equals">
/// An optional equality function. A <see langword="true"/> result suppresses the update.
/// </param>
public class Store<TValue>(TValue value, Func<TValue, TValue, bool>? equals = null)
    : Event<TValue>,
        IStore<TValue>
{
    private readonly Func<TValue, TValue, bool>? _equals = equals;
    private TValue _value = value;

    /// <summary>
    /// Converts a value into a new store holding that value.
    /// </summary>
    /// <param name="value">The initial value of the new store.</param>
    /// <returns>A new store initialized with <paramref name="value"/>.</returns>
    public static implicit operator Store<TValue>(TValue value) => new(value);

    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    /// <remarks>
    /// Assigning notifies listeners only when the new value differs from the current value.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the store has already been destroyed and a value is assigned.
    /// </exception>
    public TValue Value
    {
        get => _value;
        set
        {
            ThrowIfDestroyed();

            if (_equals is not null)
            {
                if (_equals(_value, value))
                    return;
            }
            else if (EqualityComparer<TValue>.Default.Equals(_value, value))
            {
                return;
            }

            Previous = _value;
            _value = value;

            Dispatch(_value);
        }
    }

    /// <summary>
    /// Gets the value held before the most recent change. Initially, it equals the current value.
    /// </summary>
    public TValue Previous { get; private set; } = value;

    /// <summary>
    /// Returns the string representation of the current value.
    /// </summary>
    /// <returns>
    /// The string representation of the current value, or the literal <c>"null"</c>
    /// when the current value is <see langword="null"/>.
    /// </returns>
    public override string ToString()
    {
        return _value?.ToString() ?? "null";
    }
}
