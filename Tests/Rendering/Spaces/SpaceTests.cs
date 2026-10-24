using OpenGE.Common.Lifecycle;
using OpenGE.Graphics;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Resources.Targets;
using OpenGE.Rendering;
using OpenGE.Rendering.Spaces;
using Xunit;

namespace Tests.Rendering.Spaces;

public class RenderSpaceTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_EmptyIdentifier_ThrowsArgumentException(string identifier)
    {
        Assert.Throws<ArgumentException>(() => new RenderSpace(identifier, new TestCamera()));
    }

    [Fact]
    public void Collect_PreservesEntryAndCommandOrderThenAppendsCollectedCommands()
    {
        var calls = new List<string>();
        var first = new RecordingRenderable(
            [new MarkerCommand("first-1"), new MarkerCommand("first-2")],
            _ => calls.Add("first")
        );
        var second = new RecordingRenderable(
            [new MarkerCommand("second")],
            _ => calls.Add("second")
        );
        var space = new AdditionalCommandSpace("space", new TestCamera(), calls, [first, second]);
        var context = new RenderContext(0.1, new TestCamera(), default);

        var commands = space.Collect(context);

        Assert.Equal(["first", "second", "collect"], calls);
        Assert.Collection(
            commands,
            command => Assert.Equal(new MarkerCommand("first-1"), command),
            command => Assert.Equal(new MarkerCommand("first-2"), command),
            command => Assert.Equal(new MarkerCommand("second"), command),
            command => Assert.Equal(new MarkerCommand("additional"), command)
        );
        Assert.Same(context, Assert.Single(first.Contexts));
        Assert.Same(context, Assert.Single(second.Contexts));
    }

    [Fact]
    public void Collect_ComposesAndConfiguresOnce()
    {
        var calls = new List<string>();
        var composed = new RecordingRenderable([new MarkerCommand("composed")]);
        var space = new ComposingSpace(calls, composed);
        var context = new RenderContext(0, new TestCamera(), default);

        space.Collect(context);
        space.Collect(context);

        Assert.Equal(["compose", "configure"], calls);
        Assert.Contains(composed, space.Entries);
    }

    [Fact]
    public void Destroy_ClearsEntriesAndStoresWithoutDestroyingBorrowedResources()
    {
        var entry = new RecordingRenderable([]);
        var camera = new TestCamera();
        var target = new RenderTarget(new System.Numerics.Vector2(20, 20));
        var space = new RenderSpace("space", camera, target, entries: [entry]);

        space.Destroy();

        Assert.True(space.Destroyed);
        Assert.Empty(space.Entries);
        Assert.True(space.Camera.Destroyed);
        Assert.True(space.Target.Destroyed);
        Assert.False(target.Destroyed);

        entry.Render(new RenderContext(0, camera, default));

        Assert.Single(entry.Contexts);
    }

    [Fact]
    public void Collect_AfterDestroy_ThrowsDestroyedObjectException()
    {
        var space = new RenderSpace("space", new TestCamera());
        space.Destroy();

        Assert.Throws<DestroyedObjectException>(() =>
            space.Collect(new RenderContext(0, new TestCamera(), default))
        );
    }

    private sealed class AdditionalCommandSpace(
        string identifier,
        TestCamera camera,
        List<string> calls,
        IEnumerable<OpenGE.Graphics.Interfaces.IRenderable> entries
    ) : RenderSpace(identifier, camera, entries: entries)
    {
        protected override IEnumerable<IRenderCommand> OnCollect(IRenderContext context)
        {
            calls.Add("collect");
            yield return new MarkerCommand("additional");
        }
    }

    private sealed class ComposingSpace(List<string> calls, RecordingRenderable entry)
        : RenderSpace("composing", new TestCamera())
    {
        protected override IEnumerable<OpenGE.Graphics.Interfaces.IRenderable> Compose()
        {
            calls.Add("compose");
            yield return entry;
        }

        protected override void Configure()
        {
            Assert.Same(entry, Assert.Single(Entries));
            calls.Add("configure");
        }
    }
}
