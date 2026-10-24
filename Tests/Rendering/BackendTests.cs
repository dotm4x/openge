using OpenGE.Common.Lifecycle;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Resources.Targets;
using OpenGE.Rendering;
using Xunit;

namespace Tests.Rendering;

public class RenderBackendTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_EmptyIdentifier_ThrowsArgumentException(string identifier)
    {
        Assert.Throws<ArgumentException>(() => new RecordingBackend(identifier));
    }

    [Fact]
    public void Render_WithoutActiveFrame_ThrowsInvalidOperationException()
    {
        var backend = new RecordingBackend();

        Assert.Throws<InvalidOperationException>(() => backend.Render([new ClearCommand()]));
    }

    [Fact]
    public void Render_ExecutesCommandsInOrderWithTargetAndContext()
    {
        var backend = new RecordingBackend();
        var target = new RenderTarget(new System.Numerics.Vector2(32, 24));
        var context = new RenderContext(0.5, new TestCamera(), default);
        var first = new MarkerCommand("first");
        var second = new MarkerCommand("second");

        backend.BeginFrame();
        backend.Render([first, second], target, context);
        backend.EndFrame();

        Assert.Equal(
            ["begin-frame", "begin-target", "command:first", "command:second", "end-frame"],
            backend.Operations
        );
        Assert.Collection(
            backend.RenderCalls,
            call =>
            {
                Assert.Same(target, call.Target);
                Assert.Same(first, call.Command);
                Assert.Same(context, call.Context);
            },
            call =>
            {
                Assert.Same(target, call.Target);
                Assert.Same(second, call.Command);
                Assert.Same(context, call.Context);
            }
        );
    }

    [Fact]
    public void BeginFrame_WhenAlreadyActive_ThrowsWithoutStartingAnotherFrame()
    {
        var backend = new RecordingBackend();
        backend.BeginFrame();

        Assert.Throws<InvalidOperationException>(backend.BeginFrame);
        Assert.Equal(1, backend.Operations.Count(operation => operation == "begin-frame"));

        backend.EndFrame();
    }

    [Fact]
    public void EndFrame_WithoutActiveFrame_IsNoOp()
    {
        var backend = new RecordingBackend();

        backend.EndFrame();

        Assert.Empty(backend.Operations);
    }

    [Fact]
    public void EndFrame_WhenBackendThrows_ResetsFrameState()
    {
        var backend = new RecordingBackend { EndFrameException = new InvalidOperationException() };
        backend.BeginFrame();

        Assert.Throws<InvalidOperationException>(backend.EndFrame);

        backend.EndFrameException = null;
        backend.BeginFrame();
        backend.EndFrame();

        Assert.Equal(2, backend.Operations.Count(operation => operation == "begin-frame"));
    }

    [Fact]
    public void Render_NullCommands_ThrowsArgumentNullException()
    {
        var backend = new RecordingBackend();
        backend.BeginFrame();

        Assert.Throws<ArgumentNullException>(() => backend.Render(null!));

        backend.EndFrame();
    }

    [Fact]
    public void Render_DestroyedTarget_ThrowsObjectDisposedException()
    {
        var backend = new RecordingBackend();
        var target = new RenderTarget(new System.Numerics.Vector2(32, 24));
        target.Destroy();
        backend.BeginFrame();

        Assert.Throws<ObjectDisposedException>(() => backend.Render([], target));

        backend.EndFrame();
    }

    [Fact]
    public void Destroy_PublicOperationsThrowDestroyedObjectException()
    {
        var backend = new RecordingBackend();
        backend.Destroy();

        Assert.Throws<DestroyedObjectException>(backend.BeginFrame);
        Assert.Throws<DestroyedObjectException>(backend.EndFrame);
        Assert.Throws<DestroyedObjectException>(() => backend.Render([]));
    }
}
