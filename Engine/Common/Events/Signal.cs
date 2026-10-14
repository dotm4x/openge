using OpenGE.Common.Lifecycle;

namespace OpenGE.Common.Events;

/// <summary>
/// Represents a dispatcher for specific events, allowing listeners to be notified
/// when the signal fires.
/// </summary>
/// <typeparam name="TPayload">The type of the value passed to signal listeners.</typeparam>
/// <example>
/// <code>
/// var onClick = new Signal&lt;ClickData&gt;();
///
/// onClick.Connect(data =>
/// {
///     Console.WriteLine($"Clicked {data.Id} {data.Count} times");
/// });
///
/// onClick.Fire(new ClickData("btn-submit", 3));
/// </code>
/// </example>
public class Signal<TPayload> : Event<TPayload>
{
    /// <summary>
    /// Dispatches the signal, invoking all connected listener callbacks
    /// with the provided payload.
    /// </summary>
    /// <param name="payload">The value passed to each event listener.</param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the signal has already been destroyed.
    /// </exception>
    public void Fire(TPayload payload)
    {
        Dispatch(payload);
    }
}
