using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common.Lifecycle;

public class PoolTests
{
    [Fact]
    public void Acquire_WhenItemWasReleased_ReusesAndAcquiresIt()
    {
        var factoryCount = 0;
        var pool = new Pool<TestPoolable>(() =>
        {
            factoryCount++;
            return new TestPoolable();
        });
        var item = pool.Acquire();
        pool.Release(item);

        var reused = pool.Acquire();

        Assert.Same(item, reused);
        Assert.Equal(1, factoryCount);
        Assert.Equal(2, item.AcquireCount);
        Assert.Equal(1, item.ReleaseCount);
        Assert.Equal(1, item.RestoreCount);
    }

    [Fact]
    public void Release_SameAvailableItemTwice_ThrowsInvalidOperationException()
    {
        var pool = new Pool<TestPoolable>(() => new TestPoolable());
        var item = pool.Acquire();
        pool.Release(item);

        Assert.Throws<InvalidOperationException>(() => pool.Release(item));
    }

    [Fact]
    public void Release_DestroyedItem_ThrowsInvalidOperationException()
    {
        var pool = new Pool<TestPoolable>(() => new TestPoolable());
        var item = pool.Acquire();
        item.Destroy();

        Assert.Throws<InvalidOperationException>(() => pool.Release(item));
    }

    [Fact]
    public void Acquire_WhenOnAcquireThrows_ReturnsItemToPool()
    {
        var item = new TestPoolable { FailAcquire = true };
        var factoryCount = 0;
        var pool = new Pool<TestPoolable>(() =>
        {
            factoryCount++;
            return item;
        });

        Assert.Throws<InvalidOperationException>(() => pool.Acquire());
        item.FailAcquire = false;

        Assert.Same(item, pool.Acquire());
        Assert.Equal(1, factoryCount);
    }

    [Fact]
    public void Destroy_DestroysAvailableItemsButNotAcquiredItems()
    {
        var pool = new Pool<TestPoolable>(() => new TestPoolable());
        var available = pool.Acquire();
        var acquired = pool.Acquire();
        pool.Release(available);

        pool.Destroy();

        Assert.True(available.Destroyed);
        Assert.False(acquired.Destroyed);
        Assert.Throws<DestroyedObjectException>(() => pool.Acquire());
        Assert.Throws<DestroyedObjectException>(() => pool.Release(acquired));
    }

    private sealed class TestPoolable : Destroyable, IPoolable
    {
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public int RestoreCount { get; private set; }
        public bool FailAcquire { get; set; }

        public void OnAcquire()
        {
            AcquireCount++;
            if (FailAcquire)
                throw new InvalidOperationException();
        }

        public void OnRelease()
        {
            ReleaseCount++;
        }

        public void Restore()
        {
            RestoreCount++;
        }
    }
}
