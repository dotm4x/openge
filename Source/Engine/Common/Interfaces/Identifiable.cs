namespace OpenGE.Common.Interfaces;

/// <summary>
/// Represents an object that can be uniquely identified by a value of a specified type.
/// </summary>
/// <typeparam name="TIdentifier">
/// The type of value used to uniquely identify the object.
/// </typeparam>
public interface IIdentifiable<out TIdentifier>
{
    /// <summary>
    /// Gets the unique identifier of the object.
    /// </summary>
    TIdentifier Identifier { get; }
}
