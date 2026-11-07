using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using OpenGE.Logging;
using OpenGE.Noding;
using Xunit;

namespace Tests.Noding;

public class NodeManagerTests
{
    [Fact]
    public void Constructor_RegistersInitialAndAdditionalRootsByInitialName()
    {
        var initial = new Node("initial");
        var additional = new Node("additional");
        var manager = new NodeManager(initial, [additional]);
        initial.Name.Set("renamed");

        Assert.Same(initial, manager.ActiveRoot.Get());
        Assert.Same(initial, manager.Roots["initial"]);
        Assert.False(manager.Roots.ContainsKey("renamed"));
        Assert.Same(additional, manager.Roots["additional"]);
    }

    [Fact]
    public void Constructor_DuplicateRootNames_ThrowsAndDoesNotDestroyCallerNodes()
    {
        var first = new Node("same");
        var second = new Node("same");

        Assert.Throws<ArgumentException>(() => new NodeManager(first, [second]));

        Assert.False(first.Destroyed);
        Assert.False(second.Destroyed);
        var parent = new Node("parent");
        parent.Subnodes.Add(first);
        Assert.Same(parent, first.Parent.Get());
    }

    [Fact]
    public void Switch_WhileIdle_ChangesSelectionWithoutLoading()
    {
        var initial = new TrackingNode("initial");
        var next = new TrackingNode("next");
        var manager = new NodeManager(initial, [next]);

        manager.Switch("next");

        Assert.Same(next, manager.ActiveRoot.Get());
        Assert.False(initial.Loaded);
        Assert.False(next.Loaded);
        Assert.Equal(0, next.LoadCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Switch_WhitespaceIdentifier_ThrowsArgumentException(string identifier)
    {
        var manager = new NodeManager(new Node("initial"));

        Assert.Throws<ArgumentException>(() => manager.Switch(identifier));
    }

    [Fact]
    public void Switch_UnknownRoot_ThrowsInvalidOperationException()
    {
        var manager = new NodeManager(new Node("initial"));

        Assert.Throws<InvalidOperationException>(() => manager.Switch("missing"));
    }

    [Fact]
    public void RemoveActiveRoot_RestoresRegistrationAndThrows()
    {
        var initial = new Node("initial");
        var manager = new NodeManager(initial);

        Assert.Throws<InvalidOperationException>(() => manager.Roots.Remove("initial"));

        Assert.Same(initial, manager.Roots["initial"]);
        Assert.Same(initial, manager.ActiveRoot.Get());
    }

    [Fact]
    public void ClearRoots_WhenRootsExist_ThrowsWithoutChangingContents()
    {
        var initial = new Node("initial");
        var manager = new NodeManager(initial);

        Assert.Throws<InvalidOperationException>(manager.Roots.Clear);

        Assert.Single(manager.Roots);
        Assert.Same(initial, manager.Roots["initial"]);
    }

    [Fact]
    public void RemoveInactiveRoot_ReturnsItToCaller()
    {
        var initial = new Node("initial");
        var inactive = new Node("inactive");
        var manager = new NodeManager(initial, [inactive]);

        Assert.True(manager.Roots.Remove("inactive"));

        var parent = new Node("parent");
        parent.Subnodes.Add(inactive);
        Assert.Same(parent, inactive.Parent.Get());
    }

    [Fact]
    public void AddInvalidRoot_RollsBackDictionaryInsertion()
    {
        var initial = new Node("initial");
        var parent = new Node("parent");
        var child = new Node("child", new NodeOptions { Parent = parent });
        var manager = new NodeManager(initial);

        Assert.Throws<InvalidOperationException>(() => manager.Roots.Add("child", child));

        Assert.False(manager.Roots.ContainsKey("child"));
        Assert.Same(parent, child.Parent.Get());
    }

    [Fact]
    public void AddRootAlreadyRegisteredByAnotherManager_IsRejected()
    {
        var sharedRoot = new Node("shared");
        var firstManager = new NodeManager(new Node("first"), [sharedRoot]);
        var secondManager = new NodeManager(new Node("second"));

        Assert.Throws<InvalidOperationException>(() =>
            secondManager.Roots.Add("shared", sharedRoot)
        );

        Assert.False(secondManager.Roots.ContainsKey("shared"));
        Assert.Same(sharedRoot, firstManager.Roots["shared"]);
    }

    [Fact]
    public void AddDestroyedRoot_IsRejectedWithoutRegistering()
    {
        var initial = new Node("initial");
        var destroyed = new Node("destroyed");
        destroyed.Destroy();
        var manager = new NodeManager(initial);

        Assert.Throws<InvalidOperationException>(() => manager.Roots.Add("destroyed", destroyed));

        Assert.False(manager.Roots.ContainsKey("destroyed"));
    }

    [Fact]
    public void AddLoadedRoot_IsRejectedWithoutRegistering()
    {
        var initial = new Node("initial");
        var loaded = new Node("loaded");
        loaded.Load();
        var manager = new NodeManager(initial);

        Assert.Throws<InvalidOperationException>(() => manager.Roots.Add("loaded", loaded));

        Assert.False(manager.Roots.ContainsKey("loaded"));
        Assert.True(loaded.Loaded);
    }

    [Fact]
    public void ReplaceInactiveRoot_TransfersRegistrationWithoutDestroyingOldRoot()
    {
        var initial = new Node("initial");
        var oldRoot = new Node("old");
        var newRoot = new Node("new");
        var manager = new NodeManager(initial, [oldRoot]);

        manager.Roots["old"] = newRoot;

        Assert.Same(newRoot, manager.Roots["old"]);
        Assert.False(oldRoot.Destroyed);
        var parent = new Node("parent");
        parent.Subnodes.Add(oldRoot);
        Assert.Same(parent, oldRoot.Parent.Get());
    }

    [Fact]
    public void ReplaceActiveRoot_RestoresPreviousValueAndThrows()
    {
        var initial = new Node("initial");
        var replacement = new Node("replacement");
        var manager = new NodeManager(initial);

        Assert.Throws<InvalidOperationException>(() => manager.Roots["initial"] = replacement);

        Assert.Same(initial, manager.Roots["initial"]);
        Assert.False(replacement.Destroyed);
    }

    [Fact]
    public void StartAndStop_LoadAndUnloadActiveRoot()
    {
        var root = new TrackingNode("root");
        var manager = new NodeManager(root);
        var engine = new TestEngine(new Logger(), manager);

        engine.Start();

        Assert.True(root.Loaded);
        Assert.Equal(ModuleState.Running, manager.State.Get());

        engine.Stop();

        Assert.False(root.Loaded);
        Assert.Equal(ModuleState.Idle, manager.State.Get());
    }

    [Fact]
    public void SwitchWhileRunning_UnloadsCurrentAndLoadsSelectedRoot()
    {
        var initial = new TrackingNode("initial");
        var next = new TrackingNode("next");
        var manager = new NodeManager(initial, [next]);
        var engine = new TestEngine(new Logger(), manager);
        engine.Start();

        manager.Switch("next");

        Assert.Same(next, manager.ActiveRoot.Get());
        Assert.False(initial.Loaded);
        Assert.True(next.Loaded);
        Assert.Equal(1, initial.UnloadCount);
        Assert.Equal(1, next.LoadCount);
        engine.Stop();
    }

    [Fact]
    public void Switch_WhenNextRootLoadFails_RestoresCurrentRoot()
    {
        var initial = new TrackingNode("initial");
        var failing = new TrackingNode("failing") { FailLoad = true };
        var manager = new NodeManager(initial, [failing]);
        var engine = new TestEngine(new Logger(), manager);
        engine.Start();

        Assert.Throws<InvalidOperationException>(() => manager.Switch("failing"));

        Assert.Same(initial, manager.ActiveRoot.Get());
        Assert.True(initial.Loaded);
        Assert.False(failing.Loaded);
        Assert.Equal(2, initial.LoadCount);
        engine.Stop();
    }

    [Fact]
    public void Switch_WhenRunningToSameRoot_IsNoOp()
    {
        var initial = new TrackingNode("initial");
        var manager = new NodeManager(initial);
        var engine = new TestEngine(new Logger(), manager);
        engine.Start();

        manager.Switch("initial");

        Assert.Same(initial, manager.ActiveRoot.Get());
        Assert.Equal(1, initial.LoadCount);
        Assert.Equal(0, initial.UnloadCount);
        engine.Stop();
    }

    [Fact]
    public void AddRootWhileRunning_InjectsDependenciesBeforeLoading()
    {
        var probe = new ProbeModule();
        var initial = new RequiringNode("initial");
        var manager = new NodeManager(initial, dependencies: ["Probe"]);
        var engine = new TestEngine(new Logger(), probe, manager);
        engine.Start();
        var added = new RequiringNode("added");

        manager.Roots.Add("added", added);
        manager.Switch("added");

        Assert.Same(probe, added.InjectedProbe);
        engine.Stop();
    }

    [Fact]
    public void ReplaceInactiveRootWhileRunning_InjectsNewRootAndReturnsOldRoot()
    {
        var initial = new Node("initial");
        var old = new Node("old");
        var manager = new NodeManager(initial, [old]);
        var engine = new TestEngine(new Logger(), manager);
        engine.Start();
        var replacement = new Node("replacement");

        manager.Roots["old"] = replacement;
        manager.Switch("old");

        Assert.Same(replacement, manager.ActiveRoot.Get());
        Assert.True(replacement.Loaded);
        Assert.False(old.Destroyed);
        var parent = new Node("parent");
        parent.Subnodes.Add(old);
        Assert.Same(parent, old.Parent.Get());
        engine.Stop();
    }

    [Fact]
    public void SwitchDuringStarting_IsRejectedAndStartupRollsBack()
    {
        var alternate = new Node("alternate");
        var root = new SwitchingNode("initial", "alternate");
        var manager = new NodeManager(root, [alternate]);
        root.Manager = manager;
        var engine = new TestEngine(new Logger(), manager);

        Assert.Throws<InvalidOperationException>(engine.Start);

        Assert.False(root.Loaded);
        Assert.False(alternate.Loaded);
        Assert.Equal(ModuleState.Idle, manager.State.Get());
        Assert.Same(root, manager.ActiveRoot.Get());
    }

    [Fact]
    public void Start_ComposesAndConfiguresRootsOnlyOnce()
    {
        var calls = new List<string>();
        var composed = new Node("composed");
        var manager = new ComposingNodeManager(new Node("initial"), composed, calls);
        var engine = new TestEngine(new Logger(), manager);

        engine.Start();
        engine.Stop();
        engine.Start();
        engine.Stop();

        Assert.Equal(1, calls.Count(call => call == "compose"));
        Assert.Equal(1, calls.Count(call => call == "configure"));
        Assert.Same(composed, manager.Roots["composed"]);
    }

    [Fact]
    public void Destroy_UnloadsAndDestroysAllRegisteredRootsAndStores()
    {
        var initial = new TrackingNode("initial");
        var other = new TrackingNode("other");
        var manager = new NodeManager(initial, [other]);
        var engine = new TestEngine(new Logger(), manager);
        engine.Start();
        engine.Stop();

        manager.Destroy();

        Assert.True(initial.Destroyed);
        Assert.True(other.Destroyed);
        Assert.True(manager.Roots.Destroyed);
        Assert.True(manager.ActiveRoot.Destroyed);
        Assert.Empty(manager.Roots);
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);

    private sealed class TrackingNode(string name) : Node(name)
    {
        public bool FailLoad { get; init; }
        public int LoadCount { get; private set; }
        public int UnloadCount { get; private set; }

        protected override void OnLoad()
        {
            LoadCount++;
            if (FailLoad)
                throw new InvalidOperationException("root load failed");
        }

        protected override void OnUnload()
        {
            UnloadCount++;
        }
    }

    private sealed class ProbeModule() : Module("Probe");

    private sealed class RequiringNode(string name) : Node(name)
    {
        public ProbeModule? InjectedProbe { get; private set; }

        protected override void OnLoad()
        {
            InjectedProbe = Require<ProbeModule>();
        }
    }

    private sealed class SwitchingNode(string name, string target) : Node(name)
    {
        public NodeManager? Manager { get; set; }

        protected override void OnLoad()
        {
            Manager!.Switch(target);
        }
    }

    private sealed class ComposingNodeManager(Node initial, Node composedRoot, List<string> calls)
        : NodeManager(initial)
    {
        protected override IEnumerable<Node> Compose()
        {
            calls.Add("compose");
            yield return composedRoot;
        }

        protected override void Configure()
        {
            Assert.Same(composedRoot, Roots["composed"]);
            calls.Add("configure");
        }
    }
}
