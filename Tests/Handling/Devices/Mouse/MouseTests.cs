using System.Numerics;
using OpenGE.Common.Lifecycle;
using OpenGE.Handling.Devices.Mouse;
using OpenGE.Handling.Devices.Mouse.Events;
using Xunit;
using MouseDevice = OpenGE.Handling.Devices.Mouse.Mouse;

namespace Tests.Handling.Devices.Mouse;

public class MouseTests
{
    [Fact]
    public void PublishButton_UpdatesPositionAndFiresMatchingButtonEvents()
    {
        var left = new MouseButtonEvent("left", MouseButton.Left);
        var right = new MouseButtonEvent("right", MouseButton.Right);
        var mouse = new TestMouse(left, right);
        var leftPayloads = new List<MouseButtonEventPayload>();
        var rightPayloads = new List<MouseButtonEventPayload>();
        left.Connect(leftPayloads.Add);
        right.Connect(rightPayloads.Add);
        var payload = new MouseButtonEventPayload(
            MouseButton.Left,
            Down: true,
            Clicks: 2,
            Position: new Vector2(15, 25)
        );

        mouse.PublishButtonPayload(payload);

        Assert.Equal(payload.Position, mouse.Position.Get());
        Assert.Equal(new[] { payload }, leftPayloads);
        Assert.Empty(rightPayloads);
    }

    [Fact]
    public void PublishMove_UpdatesPositionAndNotifiesOnlyWhenMovementChanges()
    {
        var mouse = new TestMouse();
        var movements = new List<MouseMoveEventPayload>();
        mouse.OnMove.Connect(movements.Add);
        var payload = new MouseMoveEventPayload(new Vector2(10, 20), new Vector2(2, 3));

        mouse.PublishMovePayload(payload);

        Assert.Equal(payload.Position, mouse.Position.Get());
        Assert.Equal(payload, mouse.OnMove.Get());
        Assert.Equal(new[] { payload }, movements);
    }

    [Fact]
    public void PublishWheel_UpdatesPositionAndFiresWheelEvent()
    {
        var mouse = new TestMouse();
        var wheelEvents = new List<MouseWheelEventPayload>();
        mouse.OnWheel.Connect(wheelEvents.Add);
        var payload = new MouseWheelEventPayload(new Vector2(1, -2), new Vector2(30, 40));

        mouse.PublishWheelPayload(payload);

        Assert.Equal(payload.Position, mouse.Position.Get());
        Assert.Equal(new[] { payload }, wheelEvents);
    }

    [Fact]
    public void Process_InitializesCompositionAndConfigurationOnce()
    {
        var composedEvent = new MouseButtonEvent("composed", MouseButton.Middle);
        var mouse = new ComposingMouse(composedEvent);
        var receivedCount = 0;
        composedEvent.Connect(_ => receivedCount++);

        mouse.ProcessPayload(new MouseButtonEventPayload(MouseButton.Middle, true, 1, Vector2.One));
        mouse.ProcessPayload(
            new MouseButtonEventPayload(MouseButton.Middle, false, 1, Vector2.Zero)
        );

        Assert.Equal(1, mouse.ComposeCount);
        Assert.Equal(1, mouse.ConfigureCount);
        Assert.Contains(composedEvent, mouse.Events);
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public void Destroy_DestroysOwnedEventsAndPositionStores()
    {
        var buttonEvent = new MouseButtonEvent("left", MouseButton.Left);
        var mouse = new TestMouse(buttonEvent);

        mouse.Destroy();

        Assert.True(buttonEvent.Destroyed);
        Assert.True(buttonEvent.Source.Destroyed);
        Assert.True(mouse.Events.Destroyed);
        Assert.True(mouse.OnMove.Destroyed);
        Assert.True(mouse.OnWheel.Destroyed);
        Assert.True(mouse.Position.Destroyed);
        Assert.Throws<DestroyedObjectException>(() =>
            mouse.PublishMovePayload(new MouseMoveEventPayload(Vector2.One, Vector2.One))
        );
    }

    [Fact]
    public void Constructor_DuplicateButtonEventIdentifiers_ThrowsInvalidOperationException()
    {
        var events = new[]
        {
            new MouseButtonEvent("duplicate", MouseButton.Left),
            new MouseButtonEvent("duplicate", MouseButton.Right),
        };

        Assert.Throws<InvalidOperationException>(() => new TestMouse(events));
    }

    private sealed class TestMouse(params MouseButtonEvent[] events) : MouseDevice(events)
    {
        public void ProcessPayload(MouseButtonEventPayload payload)
        {
            EnsureInitialized();
            PublishButton(payload);
        }

        public void PublishButtonPayload(MouseButtonEventPayload payload) => PublishButton(payload);

        public void PublishMovePayload(MouseMoveEventPayload payload) => PublishMove(payload);

        public void PublishWheelPayload(MouseWheelEventPayload payload) => PublishWheel(payload);
    }

    private sealed class ComposingMouse(MouseButtonEvent composedEvent) : MouseDevice
    {
        public int ComposeCount { get; private set; }
        public int ConfigureCount { get; private set; }

        public void ProcessPayload(MouseButtonEventPayload payload)
        {
            EnsureInitialized();
            PublishButton(payload);
        }

        protected override IEnumerable<MouseButtonEvent> Compose()
        {
            ComposeCount++;
            yield return composedEvent;
        }

        protected override void Configure()
        {
            ConfigureCount++;
            Assert.Contains(composedEvent, Events);
        }
    }
}
