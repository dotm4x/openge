using System.Numerics;
using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using OpenGE.Handling;
using OpenGE.Handling.Devices;
using OpenGE.Logging;
using OpenGE.Scheduling;
using OpenGE.Windowing;
using Xunit;

namespace Tests.Handling;

public class HandlerTests
{
    [Fact]
    public void Update_WhenNotRunning_ThrowsInvalidOperationException()
    {
        var handler = new TestHandler(new TestWindow());

        Assert.Throws<InvalidOperationException>(() => handler.Update(default));
    }

    [Fact]
    public void Update_ProcessesDevicesUsingWindowContext()
    {
        var window = new TestWindow();
        var device = new RecordingDevice("device");
        var handler = new TestHandler(window, [device]);
        var engine = CreateEngine(window, handler);

        engine.Start();
        handler.Update(new UpdateContext(0.016, 60));

        var context = Assert.IsType<TestContext>(Assert.Single(device.Contexts));
        Assert.Same(window, context.Window);
        engine.Stop();
        engine.Destroy();
    }

    [Fact]
    public void Start_ComposesAndConfiguresDevicesOnce()
    {
        var window = new TestWindow();
        var device = new RecordingDevice("composed");
        var handler = new ComposingHandler(window, device);
        var engine = CreateEngine(window, handler);

        engine.Start();
        handler.Update(default);
        engine.Stop();
        engine.Start();
        handler.Update(default);

        Assert.Equal(1, handler.ComposeCount);
        Assert.Equal(1, handler.ConfigureCount);
        Assert.Contains(device, handler.Devices);
        Assert.Equal(2, device.Contexts.Count);
        engine.Stop();
        engine.Destroy();
    }

    [Fact]
    public void Constructor_DuplicateDeviceIdentifiers_ThrowsInvalidOperationException()
    {
        var devices = new[] { new RecordingDevice("same"), new RecordingDevice("same") };

        Assert.Throws<InvalidOperationException>(() => new TestHandler(new TestWindow(), devices));
    }

    [Fact]
    public void Update_WhenDeviceThrows_PropagatesFailure()
    {
        var window = new TestWindow();
        var device = new RecordingDevice("failing")
        {
            ProcessException = new InvalidOperationException(),
        };
        var handler = new TestHandler(window, [device]);
        var engine = CreateEngine(window, handler);
        engine.Start();

        Assert.Throws<InvalidOperationException>(() => handler.Update(default));

        engine.Stop();
        engine.Destroy();
    }

    [Fact]
    public void Destroy_DestroysOwnedDevicesButNotBorrowedWindow()
    {
        var window = new TestWindow();
        var device = new RecordingDevice("device");
        var handler = new TestHandler(window, [device]);
        var engine = CreateEngine(window, handler);
        engine.Start();
        engine.Stop();

        handler.Destroy();

        Assert.True(device.Destroyed);
        Assert.Empty(handler.Devices);
        Assert.False(window.Destroyed);
        engine.Destroy();
    }

    private static TestEngine CreateEngine(TestWindow window, TestHandler handler)
    {
        return new TestEngine(new Logger(), new Scheduler(), new WindowManager([window]), handler);
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);

    private sealed class TestContext(Window window) : InputContext(window);

    private class TestHandler(Window window, IEnumerable<InputDevice>? devices = null)
        : InputHandler(0, window, devices)
    {
        protected override InputContext CreateContext() => new TestContext(Window);
    }

    private sealed class ComposingHandler(Window window, InputDevice device) : TestHandler(window)
    {
        public int ComposeCount { get; private set; }
        public int ConfigureCount { get; private set; }

        protected override IEnumerable<InputDevice> Compose()
        {
            ComposeCount++;
            yield return device;
        }

        protected override void Configure()
        {
            ConfigureCount++;
            Assert.Contains(device, Devices);
        }
    }

    private sealed class RecordingDevice(string identifier) : InputDevice(identifier)
    {
        public List<InputContext> Contexts { get; } = [];
        public Exception? ProcessException { get; init; }

        protected override void OnProcess(InputContext context)
        {
            Contexts.Add(context);
            if (ProcessException is not null)
                throw ProcessException;
        }
    }

    private sealed class TestWindow() : Window()
    {
        protected override void OnClose() { }

        protected override void OnModeChanged(WindowMode mode) { }

        protected override void OnMove(Vector2 position) { }

        protected override void OnOpen() { }

        protected override void OnProcess() { }

        protected override void OnResizableChanged(bool resizable) { }

        protected override void OnResize(Vector2 size) { }

        protected override void OnTitleChanged(string title) { }

        protected override void OnVisibilityChanged(bool visible) { }
    }
}
