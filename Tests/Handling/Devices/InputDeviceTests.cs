using System.Numerics;
using OpenGE.Common.Lifecycle;
using OpenGE.Handling;
using OpenGE.Handling.Devices;
using OpenGE.Scheduling;
using OpenGE.Windowing;
using Xunit;

namespace Tests.Handling.Devices;

public class InputDeviceTests
{
    [Fact]
    public void Process_InvokesDeviceWithInputContext()
    {
        var window = new TestWindow();
        var context = new TestInputContext(window);
        var device = new TestInputDevice("device");

        device.Process(context);

        Assert.Equal("device", device.Identifier);
        Assert.Same(context, Assert.Single(device.Contexts));
    }

    [Fact]
    public void Process_AfterDestroy_ThrowsDestroyedObjectException()
    {
        var device = new TestInputDevice("device");
        device.Destroy();

        Assert.Throws<DestroyedObjectException>(() =>
            device.Process(new TestInputContext(new TestWindow()))
        );
    }

    [Fact]
    public void InputEvent_Destroy_DestroysSourceStoreAndDisablesSignal()
    {
        var inputEvent = new TestInputEvent("action", 42);
        var source = inputEvent.Source;
        inputEvent.Destroy();

        Assert.True(inputEvent.Destroyed);
        Assert.True(source.Destroyed);
        Assert.Throws<DestroyedObjectException>(() => inputEvent.Fire("payload"));
        Assert.Throws<DestroyedObjectException>(() => inputEvent.Source.Set(2));
    }

    private sealed class TestInputDevice(string identifier) : InputDevice(identifier)
    {
        public List<InputContext> Contexts { get; } = [];

        protected override void OnProcess(InputContext context)
        {
            Contexts.Add(context);
        }
    }

    private sealed class TestInputEvent(string identifier, int source)
        : InputEvent<string, int>(identifier, source);

    private sealed class TestInputContext(Window window) : InputContext(window);

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
