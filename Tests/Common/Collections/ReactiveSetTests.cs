using OpenGE.Common.Collections;
using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common.Collections;

public class ReactiveSetTests
{
    [Fact]
    public void Add_AndRemove_UpdateContentsAndFireEvents()
    {
        var set = new ReactiveSet<string>();
        var added = new List<string>();
        var removed = new List<string>();
        set.OnAdd.Connect(added.Add);
        set.OnRemove.Connect(removed.Add);

        set.Add("a", "b");
        set.Remove("a");

        Assert.Equal(new[] { "b" }, set.ToArray());
        Assert.Equal(new[] { "a", "b" }, added);
        Assert.Equal(new[] { "a" }, removed);
        Assert.Equal(1, set.Count);
        Assert.True(set.Contains("b"));
    }

    [Fact]
    public void Add_WhenAtCapacity_RemovesOldestItem()
    {
        var set = new ReactiveSet<int>(limit: 2);
        var removed = new List<int>();
        set.OnRemove.Connect(removed.Add);
        set.Add(1, 2);

        set.Add(3);

        Assert.Equal(new[] { 2, 3 }, set.ToArray());
        Assert.Equal(new[] { 1 }, removed);
    }

    [Fact]
    public void Clear_FiresClearBeforeRemovalEvents()
    {
        var set = new ReactiveSet<int>([1, 2]);
        var events = new List<string>();
        set.OnClear.Connect(_ => events.Add("clear"));
        set.OnRemove.Connect(item => events.Add($"remove:{item}"));

        set.Clear();

        Assert.Equal(new[] { "clear", "remove:1", "remove:2" }, events);
        Assert.Empty(set);
    }

    [Fact]
    public void Add_DuplicateItem_ThrowsWithoutAddingDuplicate()
    {
        var set = new ReactiveSet<int>([1]);

        Assert.Throws<InvalidOperationException>(() => set.Add(1));

        Assert.Equal(new[] { 1 }, set.ToArray());
    }

    [Fact]
    public void Remove_MissingItem_ThrowsInvalidOperationException()
    {
        var set = new ReactiveSet<int>();

        Assert.Throws<InvalidOperationException>(() => set.Remove(1));
    }

    [Fact]
    public void Constructor_NegativeLimit_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReactiveSet<int>(limit: -1));
    }

    [Fact]
    public void Constructor_DuplicateInitialItems_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => new ReactiveSet<int>([1, 1]));
    }

    [Fact]
    public void Destroy_ClearsItemsAndDisablesMutations()
    {
        var set = new ReactiveSet<int>([1]);
        var notifications = 0;
        set.OnRemove.Connect(_ => notifications++);

        set.Destroy();

        Assert.True(set.Destroyed);
        Assert.Empty(set);
        Assert.Equal(0, notifications);
        Assert.Throws<DestroyedObjectException>(() => set.Add(2));
        Assert.Throws<DestroyedObjectException>(set.Clear);
        Assert.Throws<DestroyedObjectException>(() => set.Remove(1));
    }
}
