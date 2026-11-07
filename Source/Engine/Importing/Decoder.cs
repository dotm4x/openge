using OpenGE.Common;
using OpenGE.Common.Lifecycle;

namespace OpenGE.Importing;

/// <summary>
/// Represents a decoder capable of creating resources from encoded data.
/// </summary>
public abstract class Decoder : Destroyable
{
    /// <summary>
    /// Gets the type of resource produced by this decoder.
    /// </summary>
    public abstract Type ResourceType { get; }

    /// <summary>
    /// Decodes a resource from a stream.
    /// </summary>
    /// <param name="context">
    /// Information about the current importing operation.
    /// </param>
    /// <param name="stream">
    /// The stream containing the encoded resource.
    /// </param>
    /// <returns>
    /// A new resource whose ownership is transferred to the caller. The input stream is borrowed.
    /// </returns>
    protected abstract Resource OnDecode(ImportContext context, Stream stream);

    /// <summary>
    /// Decodes a resource from a stream.
    /// </summary>
    /// <param name="context">
    /// Information about the current importing operation.
    /// </param>
    /// <param name="stream">
    /// The stream containing the encoded resource.
    /// </param>
    /// <returns>
    /// A new resource whose ownership is transferred to the caller. The input stream is borrowed.
    /// </returns>
    public virtual Resource Decode(ImportContext context, Stream stream)
    {
        ThrowIfDestroyed();
        return OnDecode(context, stream);
    }

    /// <summary>
    /// Determines whether this decoder supports the provided file extension.
    /// </summary>
    /// <param name="extension">
    /// The filename extension, including its leading period.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the extension is supported; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public abstract bool Supports(string extension);
}

/// <summary>
/// Represents a decoder that creates resources of a specific type.
/// </summary>
/// <typeparam name="TResource">
/// The type of resource produced by this decoder.
/// </typeparam>
public abstract class Decoder<TResource> : Decoder
    where TResource : Resource
{
    /// <inheritdoc />
    public sealed override Type ResourceType => typeof(TResource);

    /// <inheritdoc />
    protected abstract override TResource OnDecode(ImportContext context, Stream stream);

    /// <inheritdoc />
    public override TResource Decode(ImportContext context, Stream stream)
    {
        return (TResource)base.Decode(context, stream);
    }
}
