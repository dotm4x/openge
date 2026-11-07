using OpenGE.Common.Events;
using OpenGE.Common.Lifecycle;
using Xunit;

namespace Tests.Common.Events;

public class SignalTests
{
    [Fact]
    public void Fire_InvokesConnectedListenersWithPayload()
    {
        var signal = new Signal<string>();
        var received = new List<string>();
        signal.Connect(received.Add);
        signal.Connect(value => received.Add(value.ToUpperInvariant()));

        signal.Fire("hello");

        Assert.Equal(new[] { "hello", "HELLO" }, received);
    }

    [Fact]
    public void Disconnect_CalledMoreThanOnce_RemainsDisconnected()
    {
        var signal = new Signal<int>();
        var received = 0;
        var connection = signal.Connect(_ => received++);

        connection.Disconnect();
        connection.Disconnect();
        signal.Fire(1);

        Assert.False(connection.Connected);
        Assert.Equal(0, received);
    }

    [Fact]
    public void Connect_WithNullCallback_ThrowsArgumentNullException()
    {
        var signal = new Signal<int>();

        Assert.Throws<ArgumentNullException>(() => signal.Connect(null!));
    }

    [Fact]
    public void Clear_WithoutForce_PreservesPersistentConnections()
    {
        var signal = new Signal<int>();
        var received = new List<int>();
        var regular = signal.Connect(received.Add);
        var persistent = signal.Connect(received.Add, persistent: true);

        signal.Clear();
        signal.Fire(4);

        Assert.False(regular.Connected);
        Assert.True(persistent.Connected);
        Assert.Equal(new[] { 4 }, received);
    }

    [Fact]
    public void Clear_WithForce_RemovesPersistentConnections()
    {
        var signal = new Signal<int>();
        var connection = signal.Connect(_ => { }, persistent: true);

        signal.Clear(force: true);

        Assert.False(connection.Connected);
    }

    [Fact]
    public void Fire_WhenListenerDisconnectsAnother_SkipsDisconnectedListener()
    {
        var signal = new Signal<int>();
        var received = new List<string>();
        EventConnection<int>? second = null;
        signal.Connect(_ =>
        {
            received.Add("first");
            second!.Disconnect();
        });
        second = signal.Connect(_ => received.Add("second"));

        signal.Fire(1);

        Assert.Equal(new[] { "first" }, received);
    }

    [Fact]
    public void Destroy_DetachesConnectionsAndPreventsFurtherUse()
    {
        var signal = new Signal<int>();
        var connection = signal.Connect(_ => { });

        signal.Destroy();

        Assert.True(signal.Destroyed);
        Assert.False(connection.Connected);
        Assert.Throws<DestroyedObjectException>(() => signal.Fire(1));
        Assert.Throws<DestroyedObjectException>(() => signal.Connect(_ => { }));
        Assert.Throws<DestroyedObjectException>(() => signal.Clear());
    }
}
