using OpenGE.Common.Events;
using OpenGE.Common.Interfaces;

namespace OpenGE.Handling.Devices;

/// <summary>
/// Initializes a new instance of the <see cref="InputEvent{TPayload, TSource}"/> class.
/// </summary>
/// <remarks>
/// Associates an input source with a signal that publishes its payloads.
/// </remarks>
/// <typeparam name="TPayload">The payload delivered to subscribers.</typeparam>
/// <typeparam name="TSource">The type identifying the source of this action.</typeparam>
/// <param name="identifier">The stable registration identifier.</param>
/// <param name="source">The source that activates this action.</param>
public abstract class InputEvent<TPayload, TSource>(string identifier, TSource source)
    : Signal<TPayload>,
        IIdentifiable<string>
{
    /// <summary>
    /// Gets the reactive source associated with this action.
    /// </summary>
    public readonly Store<TSource> Source = new(source);

    /// <inheritdoc />
    public string Identifier { get; } = identifier;

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();
        Source.Destroy();
    }
}
