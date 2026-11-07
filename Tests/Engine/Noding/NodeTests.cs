using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using OpenGE.Noding;
using Xunit;

namespace Tests.Noding;

public class NodeTests
{
    [Fact]
    public void Constructor_InitializesIdentityNameTagsAndSubnodes()
    {
        var child = new Node("child");
        var node = new Node("root")
        {
            Subnodes = { child },
            Tags = { "scene", "visible" },
            Persistent = true,
        };

        Assert.NotEqual(Guid.Empty, node.Identifier);
        Assert.NotEqual(node.Identifier, child.Identifier);
        Assert.Equal("root", node.Name.Value);
        Assert.True(node.Persistent);
        Assert.Same(child, node["child"]);
        Assert.Same(node, child.Parent.Value);
        Assert.Equal(new[] { "scene", "visible" }, node.Tags.ToArray());
        Assert.Equal("root/child", child.Path);
    }

    [Fact]
    public void Constructor_ObjectInitializerAssignsEverySetting()
    {
        var parent = new Node("parent");
        var child = new Node("child");
        var node = new Node
        {
            Name = "Nodo",
            Parent = parent,
            Subnodes = { child },
            Tags = { "scene", "visible" },
            Visible = false,
            Persistent = true,
        };

        Assert.Equal("Nodo", node.Name.Value);
        Assert.Same(parent, node.Parent.Value);
        Assert.Same(node, child.Parent.Value);
        Assert.Equal(["scene", "visible"], node.Tags.ToArray());
        Assert.False(node.Visible.Value);
        Assert.True(node.Persistent);
    }

    [Fact]
    public void ParentAndSubnodes_StaySynchronizedWhenParentChanges()
    {
        var firstParent = new Node("first");
        var secondParent = new Node("second");
        var child = new Node("child");

        child.Parent.Value = firstParent;

        Assert.Contains(child, firstParent.Subnodes);
        Assert.Same(firstParent, child.Parent.Value);

        child.Parent.Value = secondParent;

        Assert.DoesNotContain(child, firstParent.Subnodes);
        Assert.Contains(child, secondParent.Subnodes);
        Assert.Same(secondParent, child.Parent.Value);

        secondParent.Subnodes.Remove(child);

        Assert.Null(child.Parent.Value);
        Assert.DoesNotContain(child, secondParent.Subnodes);
    }

    [Fact]
    public void ReparentingLoadedChild_UnloadsAndLoadsAccordingToNewParent()
    {
        var firstParent = new TrackingNode("first");
        var secondParent = new TrackingNode("second");
        var child = new TrackingNode("child") { Parent = firstParent };
        firstParent.Load();

        Assert.True(child.Loaded);
        Assert.Equal(1, child.LoadCount);

        child.Parent.Value = secondParent;

        Assert.False(child.Loaded);
        Assert.Equal(1, child.UnloadCount);
        Assert.DoesNotContain(child, firstParent.Subnodes);
        Assert.Contains(child, secondParent.Subnodes);

        secondParent.Load();

        Assert.True(child.Loaded);
        Assert.Equal(2, child.LoadCount);
    }

    [Fact]
    public void Parent_WhenAssignedToSelfOrDescendant_ThrowsWithoutChangingHierarchy()
    {
        var root = new Node("root");
        var child = new Node("child") { Parent = root };
        var grandchild = new Node("grandchild") { Parent = child };

        Assert.Throws<InvalidOperationException>(() => root.Parent.Value = root);
        Assert.Throws<InvalidOperationException>(() => root.Parent.Value = grandchild);

        Assert.Null(root.Parent.Value);
        Assert.Same(root, child.Parent.Value);
        Assert.Same(child, grandchild.Parent.Value);
        Assert.Same(child, root["child"]);
    }

    [Fact]
    public void Parent_WhenParentDestroyed_RestoresPreviousParent()
    {
        var previous = new Node("previous");
        var destroyedParent = new Node("destroyed");
        var child = new Node("child") { Parent = previous };
        destroyedParent.Destroy();

        Assert.Throws<DestroyedObjectException>(() => child.Parent.Value = destroyedParent);

        Assert.Same(previous, child.Parent.Value);
        Assert.Contains(child, previous.Subnodes);
        Assert.Empty(destroyedParent.Subnodes);
    }

    [Fact]
    public void Load_ChildWithUnloadedParent_Throws()
    {
        var parent = new Node("parent");
        var child = new Node("child") { Parent = parent };

        Assert.Throws<InvalidOperationException>(child.Load);
        Assert.False(child.Loaded);
    }

    [Fact]
    public void AddingToLoadedParent_LoadsChildAndRemovingItUnloadsChild()
    {
        var parent = new TrackingNode("parent");
        var child = new TrackingNode("child");
        parent.Load();

        parent.Subnodes.Add(child);

        Assert.Same(parent, child.Parent.Value);
        Assert.True(child.Loaded);
        Assert.Equal(1, child.LoadCount);

        parent.Subnodes.Remove(child);

        Assert.Null(child.Parent.Value);
        Assert.False(child.Loaded);
        Assert.Equal(1, child.UnloadCount);
    }

    [Fact]
    public void DetachingPersistentChildFromLoadedParent_KeepsChildLoaded()
    {
        var parent = new TrackingNode("parent");
        var child = new TrackingNode("child") { Persistent = true };
        parent.Load();
        parent.Subnodes.Add(child);

        parent.Subnodes.Remove(child);

        Assert.Null(child.Parent.Value);
        Assert.True(child.Loaded);
        child.Unload();
    }

    [Fact]
    public void Path_ReflectsCurrentNamesAndAncestors()
    {
        var root = new Node("root");
        var middle = new Node("middle") { Parent = root };
        var child = new Node("child") { Parent = middle };

        Assert.Equal("root/middle/child", child.Path);
        middle.Name.Value = "renamed";
        Assert.Equal("root/renamed/child", child.Path);
        Assert.Contains("root/renamed/child", child.ToString());
    }

    [Fact]
    public void NodeReactiveSet_SearchesByIdentifierNamePathAndTag()
    {
        var root = new Node("root");
        var first = new SpecializedNode("first") { Parent = root, Tags = { "shared" } };
        var middle = new Node("middle") { Parent = root };
        var nested = new SpecializedNode("nested") { Parent = middle, Tags = { "shared" } };

        Assert.Same(first, root.Subnodes.GetByName("first"));
        Assert.Null(root.Subnodes.GetByName("nested"));
        Assert.Same(nested, root.Subnodes.GetByName("nested", recursive: true));
        Assert.Null(root.Subnodes.GetByIdentifier(nested.Identifier));
        Assert.Same(nested, root.Subnodes.GetByIdentifier(nested.Identifier, recursive: true));
        Assert.Same(
            nested,
            root.Subnodes.GetByIdentifier<SpecializedNode>(nested.Identifier, true)
        );
        Assert.Null(root.Subnodes.GetByIdentifier<SpecializedNode>(middle.Identifier));
        Assert.Same(nested, root.Subnodes.GetByName<SpecializedNode>("nested", true));
        Assert.Null(root.Subnodes.GetByName<SpecializedNode>("middle", true));
        Assert.Equal(new[] { first }, root.Subnodes.GetByTag("shared"));
        Assert.Equal(new[] { first, nested }, root.Subnodes.GetByTag("shared", recursive: true));
        Assert.Equal(
            new[] { first, nested },
            root.Subnodes.GetByTag<SpecializedNode>("shared", recursive: true)
        );
    }

    [Fact]
    public void NodeReactiveSet_ResolvesRelativePathsAndParentSegments()
    {
        var root = new Node("root");
        var child = new Node("child") { Parent = root };
        var nested = new Node("nested") { Parent = child };

        Assert.Same(root, root.Subnodes.GetByPath("."));
        Assert.Same(child, root.Subnodes.GetByPath("child"));
        Assert.Same(nested, root.Subnodes.GetByPath("child/nested"));
        Assert.Same(root, child.Subnodes.GetByPath(".."));
        Assert.Same(nested, child.Subnodes.GetByPath("./nested"));
        Assert.Null(root.Subnodes.GetByPath("missing"));
        Assert.Null(root.Subnodes.GetByPath("../"));
        Assert.Same(nested, root.Subnodes.GetByPath<Node>("child/nested"));
        Assert.Null(root.Subnodes.GetByPath<SpecializedNode>("child/nested"));
    }

    [Fact]
    public void PathAndRecursiveSearch_HandleDeepHierarchiesWithoutRecursiveCalls()
    {
        const int depth = 2500;
        var root = new Node("root");
        var current = root;

        for (var index = 0; index < depth; index++)
        {
            var child = new Node($"node{index}");
            current.Subnodes.Add(child);
            current = child;
        }

        Assert.EndsWith($"node{depth - 1}", current.Path);
        Assert.Same(current, root.Subnodes.GetByName($"node{depth - 1}", recursive: true));
        Assert.Same(current, root.Subnodes.GetByIdentifier(current.Identifier, recursive: true));
    }

    [Fact]
    public void Load_ProcessesParentBeforeChildrenAndUnloadReversesOrder()
    {
        var calls = new List<string>();
        var root = new TrackingNode("root", calls: calls);
        var child = new TrackingNode("child", calls) { Parent = root };
        var grandchild = new TrackingNode("grandchild", calls) { Parent = child };

        root.Load();

        Assert.Equal(new[] { "load:root", "load:child", "load:grandchild" }, calls);
        Assert.True(root.Loaded);
        Assert.True(child.Loaded);
        Assert.True(grandchild.Loaded);
        Assert.Throws<InvalidOperationException>(root.Load);

        root.Unload();

        Assert.Equal(
            new[]
            {
                "load:root",
                "load:child",
                "load:grandchild",
                "unload:grandchild",
                "unload:child",
                "unload:root",
            },
            calls
        );
        Assert.False(root.Loaded);
        Assert.False(child.Loaded);
        Assert.False(grandchild.Loaded);
        Assert.Throws<InvalidOperationException>(root.Unload);
    }

    [Fact]
    public void Unload_PreservesLoadedPersistentChildren()
    {
        var root = new TrackingNode("root");
        var regular = new TrackingNode("regular") { Parent = root };
        var persistent = new TrackingNode("persistent") { Parent = root, Persistent = true };

        root.Load();
        root.Unload();

        Assert.False(root.Loaded);
        Assert.False(regular.Loaded);
        Assert.True(persistent.Loaded);
        Assert.Equal(0, persistent.UnloadCount);

        persistent.Unload();
        Assert.False(persistent.Loaded);
    }

    [Fact]
    public void Load_WhenChildLoadFails_RollsBackNewlyLoadedNodes()
    {
        var calls = new List<string>();
        var root = new TrackingNode("root", calls: calls);
        var successfulChild = new TrackingNode("successful", calls) { Parent = root };
        var failingChild = new TrackingNode("failing", calls) { Parent = root, FailLoad = true };

        Assert.Throws<InvalidOperationException>(root.Load);

        Assert.False(root.Loaded);
        Assert.False(successfulChild.Loaded);
        Assert.False(failingChild.Loaded);
        Assert.Equal(
            new[]
            {
                "load:root",
                "load:successful",
                "load:failing",
                "unload:successful",
                "unload:root",
            },
            calls
        );
    }

    [Fact]
    public void Load_WhenChildLoadFails_PreservesPreviouslyLoadedPersistentChildren()
    {
        var root = new TrackingNode("root");
        var persistent = new TrackingNode("persistent") { Parent = root, Persistent = true };
        root.Load();
        root.Unload();
        var failing = new TrackingNode("failing") { Parent = root, FailLoad = true };

        Assert.Throws<InvalidOperationException>(root.Load);

        Assert.False(root.Loaded);
        Assert.True(persistent.Loaded);
        Assert.False(failing.Loaded);
        Assert.Equal(1, persistent.LoadCount);
    }

    [Fact]
    public void Load_WhenOnLoadFails_DoesNotCallOnUnloadForIncompleteInitialization()
    {
        var node = new TrackingNode("node") { FailLoad = true };

        Assert.Throws<InvalidOperationException>(node.Load);

        Assert.False(node.Loaded);
        Assert.Equal(1, node.LoadCount);
        Assert.Equal(0, node.UnloadCount);
    }

    [Fact]
    public void Load_WhenRollbackCleanupFails_AggregatesAndCanBeRetried()
    {
        var root = new TrackingNode("root");
        var child = new TrackingNode("child") { Parent = root, FailUnload = true };
        var failing = new TrackingNode("failing") { Parent = root, FailLoad = true };

        var exception = Assert.Throws<AggregateException>(root.Load);

        Assert.Contains(exception.InnerExceptions, inner => inner.Message == "load failure");
        Assert.Contains(exception.InnerExceptions, inner => inner.Message == "unload failure");
        Assert.True(root.Loaded);
        Assert.True(child.Loaded);
        Assert.False(failing.Loaded);

        child.FailUnload = false;
        root.Unload();

        Assert.False(root.Loaded);
        Assert.False(child.Loaded);
    }

    [Fact]
    public void Unload_WhenChildCleanupFails_LeavesParentLoadedForRetry()
    {
        var root = new TrackingNode("root");
        var child = new TrackingNode("child") { Parent = root, FailUnload = true };
        root.Load();

        Assert.Throws<InvalidOperationException>(root.Unload);

        Assert.True(root.Loaded);
        Assert.True(child.Loaded);
        child.FailUnload = false;
        root.Unload();

        Assert.False(root.Loaded);
        Assert.False(child.Loaded);
    }

    [Fact]
    public void Unload_WhenOwnCleanupFails_RemainsLoadedAndCanBeRetried()
    {
        var node = new TrackingNode("node") { FailUnload = true };
        node.Load();

        Assert.Throws<InvalidOperationException>(node.Unload);

        Assert.True(node.Loaded);
        node.FailUnload = false;
        node.Unload();
        Assert.False(node.Loaded);
    }

    [Fact]
    public void Load_ComposesAndConfiguresOnlyOnceAcrossLifecycleCycles()
    {
        var child = new TrackingNode("child");
        var root = new ComposingNode("root", child);

        root.Load();
        root.Unload();
        root.Load();
        root.Unload();

        Assert.Equal(1, root.ComposeCount);
        Assert.Equal(1, root.ConfigureCount);
        Assert.Equal(2, child.LoadCount);
        Assert.Same(child, root["child"]);
    }

    [Fact]
    public void Destroy_UnloadsAndDestroysDescendantsAndOwnedStores()
    {
        var root = new TrackingNode("root");
        var child = new TrackingNode("child") { Parent = root };
        root.Load();

        root.Destroy();

        Assert.True(root.Destroyed);
        Assert.True(child.Destroyed);
        Assert.False(root.Loaded);
        Assert.False(child.Loaded);
        Assert.True(root.Name.Destroyed);
        Assert.True(root.Parent.Destroyed);
        Assert.True(root.Subnodes.Destroyed);
        Assert.True(root.Tags.Destroyed);
    }

    [Fact]
    public void Destroy_WhenChildUnloadFails_DoesNotMarkParentDestroyedAndCanRetry()
    {
        var root = new TrackingNode("root");
        var child = new TrackingNode("child") { Parent = root, FailUnload = true };
        root.Load();

        Assert.Throws<InvalidOperationException>(root.Destroy);

        Assert.False(root.Destroyed);
        Assert.True(root.Loaded);
        child.FailUnload = false;
        root.Destroy();

        Assert.True(root.Destroyed);
        Assert.True(child.Destroyed);
    }

    [Fact]
    public void DestroyedNode_CannotBeLoadedOrReparented()
    {
        var node = new Node("node");
        var parent = new Node("parent");
        node.Destroy();

        Assert.Throws<DestroyedObjectException>(node.Load);
        Assert.Throws<DestroyedObjectException>(() => node.Parent.Value = parent);
    }

    [Fact]
    public void ToString_ContainsPathAndIdentifierPrefix()
    {
        var node = new Node("node");

        Assert.Equal($"Node \"node\" ({node.Identifier.ToString()[..8]})", node.ToString());
    }

    private sealed class SpecializedNode(string name) : Node(name);

    private class TrackingNode(string name, List<string>? calls = null) : Node(name)
    {
        public bool FailLoad { get; set; }
        public bool FailUnload { get; set; }
        public int LoadCount { get; private set; }
        public int UnloadCount { get; private set; }

        protected override void OnLoad()
        {
            LoadCount++;
            calls?.Add($"load:{Name.Value}");
            if (FailLoad)
                throw new InvalidOperationException("load failure");
        }

        protected override void OnUnload()
        {
            UnloadCount++;
            calls?.Add($"unload:{Name.Value}");
            if (FailUnload)
                throw new InvalidOperationException("unload failure");
        }
    }

    private sealed class ComposingNode(string name, Node composed) : TrackingNode(name)
    {
        public int ComposeCount { get; private set; }
        public int ConfigureCount { get; private set; }

        protected override IEnumerable<Node> Compose()
        {
            ComposeCount++;
            yield return composed;
        }

        protected override void Configure()
        {
            ConfigureCount++;
        }
    }
}
