namespace OpenGE.Common.Primitives;

/// <summary>
/// Represents a type with a single possible value and no associated data.
/// </summary>
public readonly struct Unit
{
    /// <summary>
    /// Gets the only value of this type.
    /// </summary>
    public static readonly Unit Value = new();
}
