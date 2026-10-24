using OpenGE.Common.Events;
using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common.Events;

public class StoreTests
{
    [Fact]
    public void Set_ChangedValue_UpdatesPreviousAndNotifiesListeners()
    {
        var store = new Store<int>(3);
        var changes = new List<(int Previous, int Current)>();
        store.Connect(current => changes.Add((store.Previous, current)));

        store.Set(5);

        Assert.Equal(5, store.Get());
        Assert.Equal(3, store.Previous);
        Assert.Equal(new[] { (3, 5) }, changes);
    }

    [Fact]
    public void Set_EqualValue_DoesNotNotifyOrChangePrevious()
    {
        var store = new Store<string>("same");
        var notificationCount = 0;
        store.Connect(_ => notificationCount++);

        store.Set(new string("same"));

        Assert.Equal("same", store.Get());
        Assert.Equal("same", store.Previous);
        Assert.Equal(0, notificationCount);
    }

    [Fact]
    public void Set_UsesCustomEquality()
    {
        var store = new Store<string>(
            "first",
            (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
        );
        var notificationCount = 0;
        store.Connect(_ => notificationCount++);

        store.Set("FIRST");

        Assert.Equal("first", store.Get());
        Assert.Equal("first", store.Previous);
        Assert.Equal(0, notificationCount);
    }

    [Fact]
    public void ToString_ReturnsCurrentValueOrNullLiteral()
    {
        var valueStore = new Store<int>(42);
        var nullStore = new Store<string?>(null);

        Assert.Equal("42", valueStore.ToString());
        Assert.Equal("null", nullStore.ToString());
    }

    [Fact]
    public void Set_AfterDestroy_ThrowsDestroyedObjectException()
    {
        var store = new Store<int>(0);
        store.Destroy();

        Assert.Throws<DestroyedObjectException>(() => store.Set(1));
        Assert.Throws<DestroyedObjectException>(() => store.Connect(_ => { }));
    }
}
