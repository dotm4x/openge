namespace OpenGE.Common.Lifecycle;

/// <summary>
/// Provides read-only access to the lifecycle state of a destroyable object.
/// </summary>
public interface IReadOnlyDestroyable
{
    /// <summary>
    /// Gets a value indicating whether the object has been destroyed.
    /// </summary>
    bool Destroyed { get; }
}

/// <summary>
/// Initializes a new instance of the <see cref="DestroyedObjectException"/> class.
/// </summary>
/// <remarks>
/// Represents an exception thrown when an operation is attempted on an object
/// that has already been destroyed.
/// </remarks>
/// <param name="message">
/// The message that describes the invalid operation.
/// </param>
public class DestroyedObjectException(string message) : InvalidOperationException(message);

/// <summary>
/// Defines an object that has a destroyable lifecycle.
/// </summary>
/// <remarks>
/// A destroyable object can be destroyed when it is no longer needed.
/// Once destroyed, the object must not be used for further operations.
/// </remarks>
public interface IDestroyable
{
    /// <summary>
    /// Gets a value indicating whether the object has been destroyed.
    /// </summary>
    bool Destroyed { get; }

    /// <summary>
    /// Destroys the object and transitions it to the destroyed state.
    /// </summary>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the object has already been destroyed.
    /// </exception>
    void Destroy();
}

/// <summary>
/// Provides a base implementation for objects with a destroyable lifecycle.
/// </summary>
/// <remarks>
/// This base class manages the destroyed state and provides a lifecycle hook
/// that derived types can override to release resources or perform cleanup.
/// Each object has one logical owner, which is responsible for calling Destroy.
/// References and injected dependencies are borrowed unless an API explicitly transfers ownership.
/// Composition transfers ownership to the composer unless its API explicitly documents borrowing.
/// Owners destroy their owned objects, never borrowed references.
/// </remarks>
public abstract class Destroyable : IDestroyable
{
    /// <summary>
    /// Destroys this object.
    /// </summary>
    /// <remarks>
    /// <see cref="OnDestroy"/> is invoked before the object is marked as destroyed.
    /// If <see cref="OnDestroy"/> throws an exception, the object remains undestroyed.
    /// Destroy owned objects through their owner; do not destroy them while still registered elsewhere.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when this object has already been destroyed.
    /// </exception>
    public void Destroy()
    {
        if (Destroyed)
            throw new DestroyedObjectException($"{GetTypeName()} has already been destroyed.");

        OnDestroy();

        Destroyed = true;
    }

    /// <inheritdoc />
    public bool Destroyed { get; private set; }

    private string GetTypeName()
    {
        var name = GetType().Name;
        var genericIndex = name.IndexOf('`');

        return genericIndex >= 0 ? name[..genericIndex] : name;
    }

    /// <summary>
    /// Performs additional cleanup when the object is destroyed.
    /// </summary>
    /// <remarks>
    /// Derived types can override this method to release resources or perform
    /// other cleanup operations required by the object.
    /// </remarks>
    protected virtual void OnDestroy() { }

    /// <summary>
    /// Throws an exception if this object has already been destroyed.
    /// </summary>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when this object has been destroyed.
    /// </exception>
    protected void ThrowIfDestroyed()
    {
        if (Destroyed)
            throw new DestroyedObjectException($"{GetTypeName()} is destroyed.");
    }
}
