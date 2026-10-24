using OpenGE.Common.Lifecycle;
using OpenGE.Handling.Devices;
using OpenGE.Handling.Devices.Keyboard;
using Xunit;
using KeyboardDevice = OpenGE.Handling.Devices.Keyboard.Keyboard;

namespace Tests.Handling.Devices.Keyboard;

public class KeyboardTests
{
    [Fact]
    public void Publish_FiresOnlyEventsMatchingPayloadKey()
    {
        var aEvent = new KeyboardEvent("a", KeyboardKey.A);
        var bEvent = new KeyboardEvent("b", KeyboardKey.B);
        var keyboard = new TestKeyboard(aEvent, bEvent);
        var receivedA = new List<KeyboardEventPayload>();
        var receivedB = new List<KeyboardEventPayload>();
        aEvent.Connect(receivedA.Add);
        bEvent.Connect(receivedB.Add);
        var payload = new KeyboardEventPayload(KeyboardKey.A, Down: true, Repeat: false);

        keyboard.PublishPayload(payload);

        Assert.Equal(new[] { payload }, receivedA);
        Assert.Empty(receivedB);
    }

    [Fact]
    public void Publish_UsesUpdatedEventSource()
    {
        var inputEvent = new KeyboardEvent("action", KeyboardKey.A);
        var keyboard = new TestKeyboard(inputEvent);
        var received = new List<KeyboardEventPayload>();
        inputEvent.Connect(received.Add);
        inputEvent.Source.Set(KeyboardKey.B);
        var payload = new KeyboardEventPayload(KeyboardKey.B, Down: false, Repeat: false);

        keyboard.PublishPayload(payload);

        Assert.Equal(new[] { payload }, received);
    }

    [Fact]
    public void Process_InitializesCompositionAndConfigurationOnce()
    {
        var composedEvent = new KeyboardEvent("composed", KeyboardKey.C);
        var keyboard = new ComposingKeyboard(composedEvent);
        var receivedCount = 0;
        composedEvent.Connect(_ => receivedCount++);

        keyboard.ProcessPayload(new KeyboardEventPayload(KeyboardKey.C, true, false));
        keyboard.ProcessPayload(new KeyboardEventPayload(KeyboardKey.C, false, false));

        Assert.Equal(1, keyboard.ComposeCount);
        Assert.Equal(1, keyboard.ConfigureCount);
        Assert.Contains(composedEvent, keyboard.Events);
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public void Destroy_DestroysRegisteredEventsAndCollection()
    {
        var inputEvent = new KeyboardEvent("action", KeyboardKey.A);
        var keyboard = new TestKeyboard(inputEvent);

        keyboard.Destroy();

        Assert.True(inputEvent.Destroyed);
        Assert.True(inputEvent.Source.Destroyed);
        Assert.True(keyboard.Events.Destroyed);
        Assert.Throws<DestroyedObjectException>(() =>
            keyboard.ProcessPayload(new KeyboardEventPayload(KeyboardKey.A, true, false))
        );
    }

    [Fact]
    public void Constructor_DuplicateEventIdentifiers_ThrowsInvalidOperationException()
    {
        var events = new[]
        {
            new KeyboardEvent("duplicate", KeyboardKey.A),
            new KeyboardEvent("duplicate", KeyboardKey.B),
        };

        Assert.Throws<InvalidOperationException>(() => new TestKeyboard(events));
    }

    private sealed class TestKeyboard(params InputEvent<KeyboardEventPayload, KeyboardKey>[] events)
        : KeyboardDevice(events)
    {
        public void ProcessPayload(KeyboardEventPayload payload)
        {
            EnsureInitialized();
            Publish(payload);
        }

        public void PublishPayload(KeyboardEventPayload payload)
        {
            Publish(payload);
        }
    }

    private sealed class ComposingKeyboard(KeyboardEvent composedEvent) : KeyboardDevice
    {
        public int ComposeCount { get; private set; }
        public int ConfigureCount { get; private set; }

        public void ProcessPayload(KeyboardEventPayload payload)
        {
            EnsureInitialized();
            Publish(payload);
        }

        protected override IEnumerable<InputEvent<KeyboardEventPayload, KeyboardKey>> Compose()
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
