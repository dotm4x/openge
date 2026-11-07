using OpenGE.Common.Lifecycle;
using OpenGE.Scheduling;
using OpenGE.Scheduling.Channels;
using OpenGE.Scheduling.Interfaces;
using Xunit;

namespace Tests.Scheduling.Channels;

public class UpdateChannelTests
{
    [Fact]
    public void Update_WithoutRateLimit_UpdatesEntriesAndPublishesTiming()
    {
        var entry = new RecordingUpdatable();
        var channel = new UpdateChannel("unlimited", entries: [entry]);

        channel.Update(0.25);

        var context = Assert.Single(entry.Contexts);
        Assert.Equal(0.25, context.DeltaTime);
        Assert.Equal(0, context.UpdateRate);
        Assert.Equal(0.25, channel.DeltaTime);
        Assert.Equal(4, channel.UpdateRate);
    }

    [Fact]
    public void Update_WithRateLimit_AccumulatesUntilIntervalAndUpdatesOnce()
    {
        var entry = new RecordingUpdatable();
        var channel = new UpdateChannel("limited", targetUpdateRate: 2, entries: [entry]);

        channel.Update(0.2);
        channel.Update(0.29);
        Assert.Empty(entry.Contexts);

        channel.Update(0.02);

        var context = Assert.Single(entry.Contexts);
        Assert.Equal(0.51, context.DeltaTime, precision: 10);
        Assert.Equal(2, context.UpdateRate);
        Assert.Equal(0.51, channel.DeltaTime, precision: 10);
        Assert.Equal(1.0 / 0.51, channel.UpdateRate, precision: 10);
    }

    [Fact]
    public void Update_InitializesCompositionAndConfigurationOnlyOnce()
    {
        var entry = new RecordingUpdatable();
        var channel = new ComposingChannel(entry);

        channel.Update(0.1);
        channel.Update(0.1);

        Assert.Equal(1, channel.ComposeCount);
        Assert.Equal(1, channel.ConfigureCount);
        Assert.Contains(entry, channel.Entries);
        Assert.Equal(2, entry.Contexts.Count);
    }

    [Fact]
    public void Update_WhenCompositionContainsDuplicateEntry_Throws()
    {
        var entry = new RecordingUpdatable();
        var channel = new ComposingChannel(entry, [entry, entry]);

        Assert.Throws<InvalidOperationException>(() => channel.Update(0.1));
        Assert.Empty(channel.Entries);
    }

    [Fact]
    public void Destroy_ClearsEntriesWithoutDestroyingBorrowedUpdatable()
    {
        var entry = new DestroyableUpdatable();
        var channel = new UpdateChannel("channel", entries: [entry]);

        channel.Destroy();

        Assert.True(channel.Destroyed);
        Assert.Empty(channel.Entries);
        Assert.False(entry.Destroyed);
        Assert.Throws<DestroyedObjectException>(() => channel.Update(0.1));
    }

    private sealed class RecordingUpdatable : IUpdatable
    {
        public List<UpdateContext> Contexts { get; } = [];

        public void Update(UpdateContext context)
        {
            Contexts.Add(context);
        }
    }

    private sealed class DestroyableUpdatable : Destroyable, IUpdatable
    {
        public void Update(UpdateContext context) { }
    }

    private sealed class ComposingChannel(
        IUpdatable entry,
        IEnumerable<IUpdatable>? composedEntries = null
    ) : UpdateChannel("composing")
    {
        public int ComposeCount { get; private set; }
        public int ConfigureCount { get; private set; }

        protected override IEnumerable<IUpdatable> Compose()
        {
            ComposeCount++;
            return composedEntries ?? [entry];
        }

        protected override void Configure()
        {
            ConfigureCount++;
        }
    }
}
