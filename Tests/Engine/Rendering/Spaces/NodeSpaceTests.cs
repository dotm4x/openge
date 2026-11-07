using System.Numerics;
using OpenGE.Common;
using OpenGE.Graphics;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Logging;
using OpenGE.Noding;
using OpenGE.Rendering;
using OpenGE.Rendering.Spaces;
using Xunit;

namespace Tests.Rendering.Spaces;

public class NodeRenderSpaceTests
{
    [Fact]
    public void Collect_RendersLoadedNodesInHierarchyOrder()
    {
        var firstChild = new RenderableNode("first", "first");
        var grandchild = new RenderableNode("grandchild", "grandchild");
        firstChild.Subnodes.Add(grandchild);
        var secondChild = new RenderableNode("second", "second");
        var root = new RenderableNode("root", "root", [firstChild, secondChild]);
        var manager = new NodeManager(root);
        var engine = new TestEngine(new Logger(), manager);

        engine.Start();

        var space = new NodeRenderSpace(manager, new TestCamera());
        var commands = space.Collect(new RenderContext(0.2, new TestCamera(), default));

        Assert.Collection(
            commands,
            command => Assert.Equal(new MarkerCommand("root"), command),
            command => Assert.Equal(new MarkerCommand("first"), command),
            command => Assert.Equal(new MarkerCommand("grandchild"), command),
            command => Assert.Equal(new MarkerCommand("second"), command)
        );
        Assert.True(root.Loaded);
        Assert.True(firstChild.Loaded);
        Assert.True(grandchild.Loaded);
        Assert.True(secondChild.Loaded);

        space.Destroy();
        engine.Stop();
        engine.Destroy();
    }

    [Fact]
    public void Collect_SkipsUnloadedNodes()
    {
        var visible = new RenderableNode("visible", "visible");
        var unloaded = new RenderableNode("unloaded", "unloaded");
        var root = new Node("root", new NodeOptions { Subnodes = [visible, unloaded] });
        var manager = new NodeManager(root);
        var engine = new TestEngine(new Logger(), manager);

        engine.Start();
        unloaded.Unload();

        var space = new NodeRenderSpace(manager, new TestCamera());
        var commands = space.Collect(new RenderContext(0, new TestCamera(), default));

        Assert.Collection(commands, command => Assert.Equal(new MarkerCommand("visible"), command));

        space.Destroy();
        engine.Stop();
        engine.Destroy();
    }

    [Fact]
    public void Destroy_DoesNotDestroyBorrowedNodeManager()
    {
        var manager = new NodeManager(new Node("root"));
        var space = new NodeRenderSpace(manager, new TestCamera());

        space.Destroy();

        Assert.False(manager.Destroyed);
        Assert.False(manager.ActiveRoot.Get().Destroyed);
        manager.Destroy();
    }

    private sealed class RenderableNode(
        string name,
        string command,
        IEnumerable<Node>? children = null
    ) : Node(name, new NodeOptions { Subnodes = children }), IRenderable
    {
        public RenderData Render(IRenderContext context)
        {
            return new RenderData([new MarkerCommand(command)]);
        }
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);
}
