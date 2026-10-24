using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using OpenGE.Logging;
using OpenGE.Scheduling;
using OpenGE.Scheduling.Channels;
using OpenGE.Scheduling.Interfaces;
using Xunit;

namespace Tests.Scheduling;

public class SchedulerTests
{
    [Fact]
    public void Constructor_PreservesTargetRateAndRegistersChannels()
    {
        var channel = new UpdateChannel("main");
        var scheduler = new Scheduler(targetUpdateRate: 120, channels: [channel]);

        Assert.Equal(120, scheduler.TargetUpdateRate);
        Assert.Same(channel, scheduler.Channels["main"]);
    }

    [Fact]
    public void Constructor_DuplicateChannelIdentifiers_ThrowsInvalidOperationException()
    {
        var channels = new[] { new UpdateChannel("duplicate"), new UpdateChannel("duplicate") };

        Assert.Throws<InvalidOperationException>(() => new Scheduler(channels: channels));
    }

    [Fact]
    public void Run_WhenSchedulerIsNotRunning_ThrowsWithoutEnteringLoop()
    {
        var scheduler = new Scheduler();

        Assert.Throws<InvalidOperationException>(scheduler.Run);
    }

    [Fact]
    public void Start_PreparesInitialAndComposedChannels()
    {
        var initial = new TrackingChannel("initial");
        var composed = new TrackingChannel("composed");
        var scheduler = new ComposingScheduler(composed, [initial]);
        var engine = new TestEngine(new Logger(), scheduler);

        engine.Start();

        Assert.Equal(1, initial.ConfigureCount);
        Assert.Equal(1, composed.ConfigureCount);
        Assert.Same(initial, scheduler.Channels["initial"]);
        Assert.Same(composed, scheduler.Channels["composed"]);

        engine.Stop();
    }

    [Fact]
    public void AddingChannelWhileRunning_PreparesItImmediately()
    {
        var scheduler = new Scheduler();
        var engine = new TestEngine(new Logger(), scheduler);
        engine.Start();
        var added = new TrackingChannel("added");

        scheduler.Channels.Add(added);

        Assert.Equal(1, added.ConfigureCount);
        engine.Stop();
    }

    [Fact]
    public void Destroy_DestroysOwnedChannelsAndRejectsRun()
    {
        var channel = new UpdateChannel("owned");
        var scheduler = new Scheduler(channels: [channel]);
        var engine = new TestEngine(new Logger(), scheduler);
        engine.Start();
        engine.Stop();

        scheduler.Destroy();

        Assert.True(channel.Destroyed);
        Assert.True(scheduler.Channels.Destroyed);
        Assert.Throws<DestroyedObjectException>(scheduler.Run);
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);

    private sealed class ComposingScheduler(
        TrackingChannel composedChannel,
        IEnumerable<UpdateChannel> initialChannels
    ) : Scheduler(channels: initialChannels)
    {
        protected override IEnumerable<UpdateChannel> Compose()
        {
            yield return composedChannel;
        }
    }

    private sealed class TrackingChannel(string identifier) : UpdateChannel(identifier)
    {
        public int ConfigureCount { get; private set; }

        protected override void Configure()
        {
            ConfigureCount++;
        }
    }
}
