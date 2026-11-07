using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common;

public class ModuleTests
{
    [Fact]
    public void Constructor_PreservesIdentifierAndCopiesDependencies()
    {
        var dependencies = new List<string> { "dependency" };

        var module = new TestModule("consumer", dependencies);
        dependencies.Add("other");

        Assert.Equal("consumer", module.Identifier);
        Assert.Equal(new[] { "dependency" }, module.Dependencies);
        Assert.Equal(ModuleState.Idle, module.State.Get());
    }

    [Fact]
    public void Require_BeforeInjection_ThrowsInvalidOperationException()
    {
        var module = new TestModule("consumer");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            module.GetDependency<DependencyModule>("dependency")
        );

        Assert.Contains("has not been injected", exception.Message);
    }

    [Fact]
    public void Start_InjectsDeclaredDependencyAndExposesLifecycleStates()
    {
        var states = new List<ModuleState>();
        var dependency = new DependencyModule();
        var consumer = new TestModule("consumer", ["dependency"])
        {
            OnStarting = module =>
            {
                states.Add(module.State.Get());
                Assert.Same(dependency, module.GetDependency<DependencyModule>("dependency"));
            },
            OnStopping = module => states.Add(module.State.Get()),
        };
        var engine = new TestEngine(dependency, consumer);

        engine.Start();
        Assert.Equal(ModuleState.Running, consumer.State.Get());
        engine.Stop();

        Assert.Equal(new[] { ModuleState.Starting, ModuleState.Stopping }, states);
        Assert.Equal(ModuleState.Idle, consumer.State.Get());
    }

    [Fact]
    public void Require_UndeclaredDependency_ThrowsInvalidOperationException()
    {
        var dependency = new DependencyModule();
        var consumer = new TestModule("consumer")
        {
            OnStarting = module =>
                Assert.Contains(
                    "was not injected",
                    Assert
                        .Throws<InvalidOperationException>(() =>
                            module.GetDependency<DependencyModule>("dependency")
                        )
                        .Message
                ),
        };
        var engine = new TestEngine(dependency, consumer);

        engine.Start();
        engine.Stop();
    }

    [Fact]
    public void Require_WrongDependencyType_ThrowsInvalidOperationException()
    {
        var dependency = new DependencyModule();
        var consumer = new TestModule("consumer", ["dependency"])
        {
            OnStarting = module =>
                Assert.Contains(
                    "to be of type",
                    Assert
                        .Throws<InvalidOperationException>(() =>
                            module.GetDependency<OtherModule>("dependency")
                        )
                        .Message
                ),
        };
        var engine = new TestEngine(dependency, consumer);

        engine.Start();
        engine.Stop();
    }

    [Fact]
    public void Start_WhenOnStartThrows_CallsOnStopAndRestoresIdleState()
    {
        var calls = new List<string>();
        var module = new TestModule("module")
        {
            OnStarting = _ =>
            {
                calls.Add("start");
                throw new InvalidOperationException("start failure");
            },
            OnStopping = _ => calls.Add("stop"),
        };
        var engine = new TestEngine(module);

        Assert.Throws<InvalidOperationException>(engine.Start);

        Assert.Equal(new[] { "start", "stop" }, calls);
        Assert.Equal(ModuleState.Idle, module.State.Get());
        Assert.Equal(EngineState.Idle, engine.State.Get());
    }

    [Fact]
    public void Stop_WhenOnStopThrows_RetainsRunningStateAndCanRetry()
    {
        var stopAttempts = 0;
        var module = new TestModule("module")
        {
            OnStopping = _ =>
            {
                stopAttempts++;
                if (stopAttempts == 1)
                    throw new InvalidOperationException("stop failure");
            },
        };
        var engine = new TestEngine(module);
        engine.Start();

        Assert.Throws<InvalidOperationException>(engine.Stop);
        Assert.Equal(ModuleState.Running, module.State.Get());
        Assert.Equal(EngineState.Failed, engine.State.Get());

        engine.Stop();

        Assert.Equal(2, stopAttempts);
        Assert.Equal(ModuleState.Idle, module.State.Get());
        Assert.Equal(EngineState.Idle, engine.State.Get());
    }

    [Fact]
    public void Destroy_WhenRunning_ThrowsAndDoesNotDestroyModule()
    {
        var module = new TestModule("module");
        var engine = new TestEngine(module);
        engine.Start();

        Assert.Throws<InvalidOperationException>(module.Destroy);

        Assert.False(module.Destroyed);
        Assert.Equal(ModuleState.Running, module.State.Get());
        engine.Stop();
    }

    [Fact]
    public void Destroy_WhenIdle_DestroysModuleAndItsStateStore()
    {
        var module = new TestModule("module");

        module.Destroy();

        Assert.True(module.Destroyed);
        Assert.True(module.State.Destroyed);
        Assert.Throws<DestroyedObjectException>(() =>
            module.GetDependency<DependencyModule>("dependency")
        );
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);

    private sealed class TestModule(string identifier, IEnumerable<string>? dependencies = null)
        : Module(identifier, dependencies)
    {
        public Action<TestModule>? OnStarting { get; init; }
        public Action<TestModule>? OnStopping { get; init; }

        public TModule GetDependency<TModule>(string name)
            where TModule : Module
        {
            return Require<TModule>(name);
        }

        protected override void OnStart()
        {
            OnStarting?.Invoke(this);
        }

        protected override void OnStop()
        {
            OnStopping?.Invoke(this);
        }
    }

    private sealed class DependencyModule() : Module("dependency");

    private sealed class OtherModule() : Module("other");
}
