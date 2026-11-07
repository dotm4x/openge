using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common;

public class EngineTests
{
    [Fact]
    public void Start_ResolvesDependenciesAndStopUsesReverseOrder()
    {
        var calls = new List<string>();
        var dependency = new TestModule("dependency", calls);
        var consumer = new TestModule("consumer", calls, ["dependency"]);
        var engine = new TestEngine(calls, dependency, consumer);

        engine.Start();
        Assert.Equal(EngineState.Running, engine.State.Get());
        engine.Stop();

        Assert.Equal(
            new[]
            {
                "compose",
                "start:dependency",
                "start:consumer",
                "stop:consumer",
                "stop:dependency",
            },
            calls
        );
        Assert.Equal(EngineState.Idle, engine.State.Get());
    }

    [Fact]
    public void Start_InjectsOnlyDeclaredDependencies()
    {
        var dependency = new TestModule("dependency", []);
        var consumer = new TestModule("consumer", [], ["dependency"]);
        var engine = new TestEngine([], dependency, consumer);

        engine.Start();

        Assert.Same(dependency, consumer.GetDependency("dependency"));
        engine.Stop();
    }

    [Fact]
    public void Start_WithMissingDependency_ThrowsAndReturnsToIdle()
    {
        var consumer = new TestModule("consumer", [], ["missing"]);
        var engine = new TestEngine([], consumer);

        Assert.Throws<InvalidOperationException>(engine.Start);

        Assert.Equal(EngineState.Idle, engine.State.Get());
        Assert.Equal(ModuleState.Idle, consumer.State.Get());
    }

    [Fact]
    public void Start_WithCircularDependencies_ThrowsAndReturnsToIdle()
    {
        var first = new TestModule("first", [], ["second"]);
        var second = new TestModule("second", [], ["first"]);
        var engine = new TestEngine([], first, second);

        var exception = Assert.Throws<InvalidOperationException>(engine.Start);

        Assert.Contains("Circular dependency", exception.Message);
        Assert.Equal(EngineState.Idle, engine.State.Get());
    }

    [Fact]
    public void Start_WhenModuleFails_RollsBackStartedModulesInReverseOrder()
    {
        var calls = new List<string>();
        var first = new TestModule("first", calls);
        var second = new TestModule("second", calls) { FailStart = true };
        var engine = new TestEngine(calls, first, second);

        Assert.Throws<InvalidOperationException>(engine.Start);

        Assert.Equal(
            new[] { "compose", "start:first", "start:second", "stop:second", "stop:first" },
            calls
        );
        Assert.Equal(EngineState.Idle, engine.State.Get());
        Assert.Equal(ModuleState.Idle, first.State.Get());
        Assert.Equal(ModuleState.Idle, second.State.Get());
    }

    [Fact]
    public void ComposeAndConfigure_RunOnlyOnceAcrossStartStopCycles()
    {
        var calls = new List<string>();
        var engine = new TestEngine(calls)
        {
            ComposeModule = new TestModule("composed", calls),
            ConfigureAction = () => calls.Add("configure"),
        };

        engine.Start();
        engine.Stop();
        engine.Start();
        engine.Stop();

        Assert.Equal(1, calls.Count(call => call == "configure"));
        Assert.Equal(1, calls.Count(call => call == "compose"));
    }

    [Fact]
    public void Destroy_WhenIdle_DestroysRegisteredModulesAndEngine()
    {
        var module = new TestModule("module", []);
        var engine = new TestEngine([], module);

        engine.Destroy();

        Assert.True(engine.Destroyed);
        Assert.True(module.Destroyed);
        Assert.Throws<DestroyedObjectException>(engine.Start);
    }

    [Fact]
    public void Destroy_WhenRunning_ThrowsAndLeavesEngineRunning()
    {
        var engine = new TestEngine([], new TestModule("module", []));
        engine.Start();

        Assert.Throws<InvalidOperationException>(engine.Destroy);

        Assert.False(engine.Destroyed);
        Assert.Equal(EngineState.Running, engine.State.Get());
        engine.Stop();
    }

    private sealed class TestEngine(List<string> calls, params IEnumerable<Module> modules)
        : Engine(modules)
    {
        public Action? ConfigureAction { get; init; }
        public Module? ComposeModule { get; init; }

        protected override IEnumerable<Module> Compose()
        {
            calls.Add("compose");
            if (ComposeModule is not null)
                yield return ComposeModule;
        }

        protected override void Configure()
        {
            ConfigureAction?.Invoke();
        }
    }

    private sealed class TestModule(
        string identifier,
        List<string> calls,
        IEnumerable<string>? dependencies = null
    ) : Module(identifier, dependencies)
    {
        public bool FailStart { get; init; }

        public Module GetDependency(string name)
        {
            return Require<Module>(name);
        }

        protected override void OnStart()
        {
            calls.Add($"start:{Identifier}");
            if (FailStart)
                throw new InvalidOperationException();
        }

        protected override void OnStop()
        {
            calls.Add($"stop:{Identifier}");
        }
    }
}
