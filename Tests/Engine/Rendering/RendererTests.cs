using System.Numerics;
using OpenGE.Common;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Primitives;
using OpenGE.Graphics.Resources.Targets;
using OpenGE.Logging;
using OpenGE.Rendering;
using OpenGE.Rendering.Spaces;
using OpenGE.Scheduling;
using Xunit;

namespace Tests.Rendering;

public class RendererTests
{
    [Fact]
    public void Constructor_DuplicateBackendIdentifiers_ThrowsInvalidOperationException()
    {
        var first = new RecordingBackend("duplicate");
        var second = new RecordingBackend("duplicate");

        Assert.Throws<InvalidOperationException>(() =>
            new Renderer(0, first, new TestWindow(), backends: [second])
        );

        Assert.False(first.Destroyed);
        Assert.False(second.Destroyed);
    }

    [Fact]
    public void Update_WhenNotRunning_ThrowsInvalidOperationException()
    {
        var renderer = new Renderer(0, new RecordingBackend(), new TestWindow());

        Assert.Throws<InvalidOperationException>(() => renderer.Update(default));

        renderer.Destroy();
    }

    [Fact]
    public void SwitchBackend_ChangesActiveBackendAndRejectsUnknownIdentifiers()
    {
        var first = new RecordingBackend("first");
        var second = new RecordingBackend("second");
        var renderer = new Renderer(0, first, new TestWindow(), backends: [second]);

        renderer.SwitchBackend("second");

        Assert.Same(second, renderer.ActiveBackend.Get());
        renderer.SwitchBackend("second");
        Assert.Throws<InvalidOperationException>(() => renderer.SwitchBackend("missing"));
        Assert.Throws<ArgumentException>(() => renderer.SwitchBackend(" "));

        renderer.Destroy();
    }

    [Fact]
    public void Update_ClearsAndRendersSpacesByPriorityWithTheirViewports()
    {
        var backend = new RecordingBackend();
        var window = new TestWindow();
        window.Size.Set(new Vector2(900, 700));
        var target = new RenderTarget(new Vector2(120, 80));
        var lowCamera = new TestCamera();
        var highCamera = new TestCamera();
        var lowRenderable = new RecordingRenderable([new MarkerCommand("low")]);
        var highRenderable = new RecordingRenderable([new MarkerCommand("high")]);
        var highSpace = new RenderSpace(
            "high",
            highCamera,
            priority: RenderSpacePriority.High,
            entries: [highRenderable]
        );
        var lowSpace = new RenderSpace(
            "low",
            lowCamera,
            target,
            RenderSpacePriority.Low,
            [lowRenderable]
        );
        var renderer = new Renderer(0, backend, window, Color.Black, spaces: [highSpace, lowSpace]);
        var engine = new TestEngine(new Logger(), renderer);
        var updateContext = new UpdateContext(0.125, 60);

        engine.Start();
        renderer.Update(updateContext);

        Assert.IsType<ClearCommand>(backend.RenderCalls[0].Command);
        Assert.Null(backend.RenderCalls[0].Target);
        Assert.Equal(new MarkerCommand("low"), backend.RenderCalls[1].Command);
        Assert.Same(target, backend.RenderCalls[1].Target);
        Assert.Equal(new MarkerCommand("high"), backend.RenderCalls[2].Command);
        Assert.Null(backend.RenderCalls[2].Target);
        Assert.Null(backend.RenderCalls[0].Context);

        var lowContext = Assert.Single(lowRenderable.Contexts);
        var highContext = Assert.Single(highRenderable.Contexts);
        Assert.Equal(updateContext.DeltaTime, lowContext.DeltaTime);
        Assert.Equal(new Viewport(Vector2.Zero, target.Size), lowContext.Viewport);
        Assert.Equal(new Viewport(Vector2.Zero, window.Size.Get()), highContext.Viewport);
        Assert.Same(lowCamera, lowContext.Camera);
        Assert.Same(highCamera, highContext.Camera);
        Assert.Equal(
            [
                "begin-frame",
                "begin-window",
                "command:other",
                "begin-target",
                "command:low",
                "begin-window",
                "command:high",
                "end-frame",
            ],
            backend.Operations
        );

        engine.Stop();
        engine.Destroy();

        Assert.False(window.Destroyed);
        Assert.False(target.Destroyed);
    }

    [Fact]
    public void Update_WhenCollectionFails_StillEndsFrame()
    {
        var backend = new RecordingBackend();
        var failingEntry = new RecordingRenderable(
            [],
            _ => throw new InvalidOperationException("collection failed")
        );
        var renderer = new Renderer(
            0,
            backend,
            new TestWindow(),
            spaces: [new RenderSpace("failing", new TestCamera(), entries: [failingEntry])]
        );
        var engine = new TestEngine(new Logger(), renderer);
        engine.Start();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            renderer.Update(new UpdateContext(0, 0))
        );

        Assert.Equal("collection failed", exception.Message);
        Assert.Equal("end-frame", backend.Operations[^1]);

        engine.Stop();
        engine.Destroy();
    }

    [Fact]
    public void Update_WhenRenderingAndFrameEndFail_AggregatesBothFailures()
    {
        var backend = new RecordingBackend
        {
            EndFrameException = new InvalidOperationException("frame end failed"),
        };
        var renderException = new InvalidOperationException("render failed");
        backend.OnCommand = (_, _) => throw renderException;
        var renderer = new Renderer(0, backend, new TestWindow());
        var engine = new TestEngine(new Logger(), renderer);
        engine.Start();

        var exception = Assert.Throws<AggregateException>(() =>
            renderer.Update(new UpdateContext(0, 0))
        );

        Assert.Same(renderException, exception.InnerExceptions[0]);
        Assert.Equal("frame end failed", exception.InnerExceptions[1].Message);

        engine.Stop();
        engine.Destroy();
    }

    [Fact]
    public void Destroy_DestroysOwnedResourcesButBorrowsWindow()
    {
        var backend = new RecordingBackend();
        var window = new TestWindow();
        var target = new RenderTarget(new Vector2(10, 10));
        var space = new RenderSpace("space", new TestCamera(), target);
        var renderer = new Renderer(0, backend, window, spaces: [space]);

        renderer.Destroy();

        Assert.True(backend.Destroyed);
        Assert.True(space.Destroyed);
        Assert.False(target.Destroyed);
        Assert.True(renderer.Backends.Destroyed);
        Assert.True(renderer.Spaces.Destroyed);
        Assert.True(renderer.ClearColor.Destroyed);
        Assert.True(renderer.ActiveBackend.Destroyed);
        Assert.False(window.Destroyed);
    }

    [Fact]
    public void Start_ComposesAndConfiguresBackendsAndSpacesOnce()
    {
        var calls = new List<string>();
        var initial = new RecordingBackend("initial");
        var composed = new RecordingBackend("composed");
        var space = new RenderSpace("composed-space", new TestCamera());
        var renderer = new ComposingRenderer(initial, new TestWindow(), composed, space, calls);
        var engine = new TestEngine(new Logger(), renderer);

        engine.Start();
        engine.Stop();
        engine.Start();
        engine.Stop();

        Assert.Equal(["backends", "spaces", "configure"], calls);
        Assert.Same(composed, renderer.Backends["composed"]);
        Assert.Same(space, renderer.Spaces["composed-space"]);

        engine.Destroy();
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);

    private sealed class ComposingRenderer(
        RecordingBackend initial,
        TestWindow window,
        RecordingBackend composed,
        RenderSpace space,
        List<string> calls
    ) : Renderer(0, initial, window)
    {
        protected override IEnumerable<RenderBackend> ComposeBackends()
        {
            calls.Add("backends");
            yield return composed;
        }

        protected override IEnumerable<RenderSpace> ComposeSpaces()
        {
            calls.Add("spaces");
            yield return space;
        }

        protected override void Configure()
        {
            Assert.Same(composed, Backends["composed"]);
            Assert.Same(space, Spaces["composed-space"]);
            calls.Add("configure");
        }
    }
}
