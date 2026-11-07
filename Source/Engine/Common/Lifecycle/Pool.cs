namespace OpenGE.Common.Lifecycle;

/// <summary>
/// Defines an object that can be acquired and released by a pool.
/// </summary>
public interface IPoolable : IRestorable
{
    /// <summary>
    /// Called when the object is acquired from a pool.
    /// </summary>
    void OnAcquire();

    /// <summary>
    /// Called when the object is released back to a pool.
    /// </summary>
    void OnRelease();
}

/// <summary>
/// Initializes a new instance of the <see cref="Pool{TItem}"/> class.
/// </summary>
/// <remarks>
/// Represents a pool of reusable objects. The pool owns available items and destroys
/// those implementing IDestroyable when the pool is destroyed. Acquire transfers ownership
/// to the caller; Release transfers it back. Acquired items remain the caller's responsibility
/// even if the pool is destroyed. An item must not belong to multiple pools.
/// </remarks>
/// <typeparam name="TItem">
/// The type of objects managed by the pool.
/// </typeparam>
/// <param name="factory">
/// The factory used to create objects when no available objects exist.
/// </param>
public class Pool<TItem>(Func<TItem> factory) : Destroyable
    where TItem : IPoolable
{
    private readonly Stack<TItem> _available = [];

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        while (_available.TryPeek(out var item))
        {
            if (item is IDestroyable { Destroyed: false } destroyable)
                destroyable.Destroy();

            _available.Pop();
        }

        base.OnDestroy();
    }

    /// <summary>
    /// Acquires an object from the pool.
    /// </summary>
    /// <returns>
    /// An available object from the pool, or a newly created object when
    /// no available objects exist.
    /// </returns>
    public TItem Acquire()
    {
        ThrowIfDestroyed();

        var item = _available.Count > 0 ? _available.Pop() : factory();

        try
        {
            item.OnAcquire();
        }
        catch
        {
            _available.Push(item);
            throw;
        }

        return item;
    }

    /// <summary>
    /// Releases an object back to the pool.
    /// </summary>
    /// <param name="item">
    /// The object to release.
    /// </param>
    public void Release(TItem item)
    {
        ThrowIfDestroyed();

        if (_available.Contains(item))
            throw new InvalidOperationException("The item has already been released to this pool.");

        if (item is IDestroyable { Destroyed: true })
            throw new InvalidOperationException("A destroyed item cannot be released to the pool.");

        item.OnRelease();
        item.Restore();

        _available.Push(item);
    }
}
