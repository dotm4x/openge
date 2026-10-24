using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using OpenGE.Logging;
using OpenGE.Scheduling;
using OpenGE.Windowing;
using Xunit;

namespace Tests.Windowing;

public class WindowManagerTests
{
    [Fact]
    public void Constructor_DuplicateWindowIdentifiers_ThrowsInvalidOperationException()
    {
        var windows = new[] { new TestWindow("same"), new TestWindow("same") };

        Assert.Throws<InvalidOperationException>(() => new WindowManager(windows));
    }

    [Fact]
    public void Update_WhenNotRunning_ThrowsInvalidOperationException()
    {
        var manager = new WindowManager();

        Assert.Throws<InvalidOperationException>(() => manager.Update(default));
    }

    [Fact]
    public void Start_OpensWindowsAndUpdateForwardsContext()
    {
        var first = new TestWindow("first");
        var second = new TestWindow("second");
        var manager = new WindowManager([first, second]);
        var engine = new TestEngine(new Logger(), new Scheduler(), manager);
        var context = new UpdateContext(0.5, 20);

        engine.Start();
        Assert.True(first.Opened.Get());
        Assert.True(second.Opened.Get());
        manager.Update(context);

        Assert.Equal(1, first.ProcessCount);
        Assert.Equal(1, second.ProcessCount);
        Assert.Equal(context, first.LastContext);
        Assert.Equal(context, second.LastContext);

        engine.Stop();

        Assert.False(first.Opened.Get());
        Assert.False(second.Opened.Get());
        Assert.Throws<InvalidOperationException>(() => manager.Update(context));
    }

    [Fact]
    public void Start_WhenWindowOpenFails_ClosesPreviouslyOpenedWindows()
    {
        var first = new TestWindow("first");
        var second = new TestWindow("second") { OpenException = new InvalidOperationException() };
        var manager = new WindowManager([first, second]);
        var engine = new TestEngine(new Logger(), new Scheduler(), manager);

        Assert.Throws<InvalidOperationException>(engine.Start);

        Assert.False(first.Opened.Get());
        Assert.False(second.Opened.Get());
        Assert.Equal(1, first.CloseCount);
        Assert.Equal(ModuleState.Idle, manager.State.Get());
    }

    [Fact]
    public void Destroy_DestroysManagedWindows()
    {
        var first = new TestWindow("first");
        var second = new TestWindow("second");
        var manager = new WindowManager([first, second]);

        manager.Destroy();

        Assert.True(first.Destroyed);
        Assert.True(second.Destroyed);
        Assert.Empty(manager.Windows);
    }

    [Fact]
    public void Start_ComposesAndConfiguresWindowsOnce()
    {
        var calls = new List<string>();
        var composedWindow = new TestWindow("composed");
        var manager = new ComposingWindowManager(composedWindow, calls);
        var engine = new TestEngine(new Logger(), new Scheduler(), manager);

        engine.Start();
        engine.Stop();
        engine.Start();
        engine.Stop();

        Assert.Equal(1, calls.Count(value => value == "compose"));
        Assert.Equal(1, calls.Count(value => value == "configure"));
        Assert.Same(composedWindow, manager.Windows["composed"]);
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);

    private sealed class ComposingWindowManager(TestWindow window, List<string> calls)
        : WindowManager()
    {
        protected override IEnumerable<Window> Compose()
        {
            calls.Add("compose");
            yield return window;
        }

        protected override void Configure()
        {
            Assert.Same(window, Windows["composed"]);
            calls.Add("configure");
        }
    }

    private sealed class TestWindow(string identifier)
        : Window(new WindowOptions { Identifier = identifier, Title = identifier })
    {
        public Exception? OpenException { get; init; }
        public int CloseCount { get; private set; }
        public int ProcessCount { get; private set; }
        public UpdateContext LastContext { get; private set; }

        protected override void OnClose()
        {
            CloseCount++;
        }

        protected override void OnModeChanged(WindowMode mode) { }

        protected override void OnMove(System.Numerics.Vector2 position) { }

        protected override void OnOpen()
        {
            if (OpenException is not null)
                throw OpenException;
        }

        protected override void OnProcess()
        {
            ProcessCount++;
        }

        protected override void OnResizableChanged(bool resizable) { }

        protected override void OnResize(System.Numerics.Vector2 size) { }

        protected override void OnTitleChanged(string title) { }

        protected override void OnVisibilityChanged(bool visible) { }

        public override void Update(UpdateContext context)
        {
            LastContext = context;
            base.Update(context);
        }
    }
}
