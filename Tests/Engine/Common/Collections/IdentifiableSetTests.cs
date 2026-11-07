using OpenGE.Common.Collections;
using OpenGE.Common.Interfaces;
using Xunit;

namespace Tests.Common.Collections;

public class IdentifiableSetTests
{
    [Fact]
    public void Add_IndexesItemsByIdentifierAndFiresAddEvent()
    {
        var first = new TestItem("first", 1);
        var second = new TestItem("second", 2);
        var set = new IdentifiableSet<string, TestItem>();
        var added = new List<TestItem>();
        set.OnAdd.Connect(added.Add);

        set.Add(first, second);

        Assert.Equal(2, set.Count);
        Assert.Same(first, set["first"]);
        Assert.True(set.Contains("second"));
        Assert.True(set.Contains(second));
        Assert.Equal(new[] { first, second }, added);
    }

    [Fact]
    public void Add_DuplicateIdentifierInBatch_RejectsEntireBatch()
    {
        var set = new IdentifiableSet<string, TestItem>();
        var addedCount = 0;
        set.OnAdd.Connect(_ => addedCount++);

        Assert.Throws<InvalidOperationException>(() =>
            set.Add(new TestItem("duplicate", 1), new TestItem("duplicate", 2))
        );

        Assert.Empty(set);
        Assert.Equal(0, addedCount);
    }

    [Fact]
    public void Add_IdentifierAlreadyPresent_RejectsBatchWithoutChanges()
    {
        var existing = new TestItem("existing", 1);
        var set = new IdentifiableSet<string, TestItem>([existing]);
        var addedCount = 0;
        set.OnAdd.Connect(_ => addedCount++);

        Assert.Throws<InvalidOperationException>(() =>
            set.Add(new TestItem("new", 2), new TestItem("existing", 3))
        );

        Assert.Equal(1, set.Count);
        Assert.Same(existing, set["existing"]);
        Assert.Equal(0, addedCount);
    }

    [Fact]
    public void Add_WhenAtCapacity_RemovesOldestItem()
    {
        var first = new TestItem("first", 1);
        var second = new TestItem("second", 2);
        var third = new TestItem("third", 3);
        var set = new IdentifiableSet<string, TestItem>([first, second], limit: 2);
        var removed = new List<TestItem>();
        set.OnRemove.Connect(removed.Add);

        set.Add(third);

        Assert.Equal(2, set.Count);
        Assert.Contains(second, set);
        Assert.Contains(third, set);
        Assert.Equal(new[] { first }, removed);
    }

    [Fact]
    public void Remove_ByItem_RequiresMatchingStoredInstance()
    {
        var stored = new TestItem("item", 1);
        var set = new IdentifiableSet<string, TestItem>([stored]);

        Assert.Throws<InvalidOperationException>(() => set.Remove(new TestItem("item", 2)));
        set.Remove(stored);

        Assert.Empty(set);
    }

    [Fact]
    public void TryGetValue_ReturnsItemWhenIdentifierExists()
    {
        var item = new TestItem("item", 1);
        var set = new IdentifiableSet<string, TestItem>([item]);

        Assert.True(set.TryGetValue("item", out var result));
        Assert.Same(item, result);
        Assert.False(set.TryGetValue("missing", out _));
    }

    [Fact]
    public void Clear_FiresClearThenRemovalEvents()
    {
        var set = new IdentifiableSet<string, TestItem>([new("one", 1), new("two", 2)]);
        var events = new List<string>();
        set.OnClear.Connect(_ => events.Add("clear"));
        set.OnRemove.Connect(item => events.Add($"remove:{item.Identifier}"));

        set.Clear();

        Assert.Equal(new[] { "clear", "remove:one", "remove:two" }, events);
        Assert.Empty(set);
    }

    private sealed record TestItem(string Identifier, int Value) : IIdentifiable<string>;
}
