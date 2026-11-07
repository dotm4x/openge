using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common.Lifecycle;

public class DestroyableTests
{
    [Fact]
    public void Destroy_CalledOnce_RunsCleanupAndMarksDestroyed()
    {
        var destroyable = new TestDestroyable();

        destroyable.Destroy();

        Assert.True(destroyable.Destroyed);
        Assert.Equal(1, destroyable.DestroyCount);
    }

    [Fact]
    public void Destroy_CalledTwice_ThrowsDestroyedObjectException()
    {
        var destroyable = new TestDestroyable();
        destroyable.Destroy();

        Assert.Throws<DestroyedObjectException>(destroyable.Destroy);

        Assert.Equal(1, destroyable.DestroyCount);
    }

    [Fact]
    public void Destroy_WhenCleanupThrows_RemainsUsableForRetry()
    {
        var destroyable = new TestDestroyable { FailCleanup = true };

        Assert.Throws<InvalidOperationException>(destroyable.Destroy);
        Assert.False(destroyable.Destroyed);
        destroyable.FailCleanup = false;

        destroyable.Destroy();

        Assert.True(destroyable.Destroyed);
        Assert.Equal(2, destroyable.DestroyCount);
    }

    [Fact]
    public void ProtectedGuard_AfterDestroy_ThrowsDestroyedObjectException()
    {
        var destroyable = new TestDestroyable();
        destroyable.Destroy();

        Assert.Throws<DestroyedObjectException>(destroyable.CheckDestroyed);
    }

    private sealed class TestDestroyable : Destroyable
    {
        public int DestroyCount { get; private set; }
        public bool FailCleanup { get; set; }

        public void CheckDestroyed()
        {
            ThrowIfDestroyed();
        }

        protected override void OnDestroy()
        {
            DestroyCount++;
            if (FailCleanup)
                throw new InvalidOperationException();
        }
    }
}
