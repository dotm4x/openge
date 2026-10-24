using OpenGE.Common.Collections;
using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common.Collections;

public class ReactiveDictionaryTests
{
    [Fact]
    public void Add_AndRemove_UpdateEntriesAndFireEvents()
    {
        var dictionary = new ReactiveDictionary<string, int>();
        var added = new List<KeyValuePair<string, int>>();
        var removed = new List<KeyValuePair<string, int>>();
        dictionary.OnAdd.Connect(added.Add);
        dictionary.OnRemove.Connect(removed.Add);

        dictionary.Add("one", 1);
        Assert.True(dictionary.Remove("one"));

        Assert.Empty(dictionary);
        Assert.Equal(new[] { new KeyValuePair<string, int>("one", 1) }, added);
        Assert.Equal(new[] { new KeyValuePair<string, int>("one", 1) }, removed);
        Assert.False(dictionary.Remove("missing"));
    }

    [Fact]
    public void Indexer_AddsMissingKeyAndUpdatesExistingKey()
    {
        var dictionary = new ReactiveDictionary<string, int>();
        var added = new List<KeyValuePair<string, int>>();
        var updates =
            new List<(KeyValuePair<string, int> Previous, KeyValuePair<string, int> Current)>();
        dictionary.OnAdd.Connect(added.Add);
        dictionary.OnUpdate.Connect(updates.Add);

        dictionary["one"] = 1;
        dictionary["one"] = 2;

        Assert.Equal(2, dictionary["one"]);
        Assert.Equal(new[] { new KeyValuePair<string, int>("one", 1) }, added);
        Assert.Equal(
            new[]
            {
                (new KeyValuePair<string, int>("one", 1), new KeyValuePair<string, int>("one", 2)),
            },
            updates
        );
    }

    [Fact]
    public void Clear_FiresClearBeforeEntryRemovalEvents()
    {
        var dictionary = new ReactiveDictionary<string, int>([new("one", 1), new("two", 2)]);
        var events = new List<string>();
        dictionary.OnClear.Connect(_ => events.Add("clear"));
        dictionary.OnRemove.Connect(entry => events.Add($"remove:{entry.Key}"));

        dictionary.Clear();

        Assert.Equal(new[] { "clear", "remove:one", "remove:two" }, events);
        Assert.Empty(dictionary);
    }

    [Fact]
    public void Constructor_DuplicateKeys_ThrowsArgumentException()
    {
        var entries = new[] { new KeyValuePair<string, int>("one", 1), new("one", 2) };

        Assert.Throws<ArgumentException>(() => new ReactiveDictionary<string, int>(entries));
    }

    [Fact]
    public void LookupMethods_ReturnExpectedResults()
    {
        var dictionary = new ReactiveDictionary<string, int>([new("one", 1)]);

        Assert.True(dictionary.ContainsKey("one"));
        Assert.True(dictionary.ContainsValue(1));
        Assert.True(dictionary.TryGetValue("one", out var value));
        Assert.Equal(1, value);
        Assert.False(dictionary.TryGetValue("missing", out _));
    }

    [Fact]
    public void Destroy_ClearsEntriesAndDisablesMutations()
    {
        var dictionary = new ReactiveDictionary<string, int>([new("one", 1)]);
        var notifications = 0;
        dictionary.OnRemove.Connect(_ => notifications++);

        dictionary.Destroy();

        Assert.True(dictionary.Destroyed);
        Assert.Empty(dictionary);
        Assert.Equal(0, notifications);
        Assert.Throws<DestroyedObjectException>(() => dictionary.Add("two", 2));
        Assert.Throws<DestroyedObjectException>(() => dictionary["two"] = 2);
        Assert.Throws<DestroyedObjectException>(() => dictionary.Remove("one"));
        Assert.Throws<DestroyedObjectException>(dictionary.Clear);
    }
}
