using OpenGE.Noding;
using OpenGE.Scheduling;
using OpenGE.Scheduling.Channels;
using OpenGE.Scheduling.Interfaces;
using Xunit;

namespace Tests.Scheduling.Channels;

public class NodeUpdateChannelTests
{
    [Fact]
    public void Update_VisitsLoadedUpdatableNodesInActiveRootTree()
    {
        var root = new RecordingNode("root");
        var child = new RecordingNode("child", new NodeOptions { Parent = root });
        var unloadedChild = new RecordingNode("unloaded", new NodeOptions { Parent = root });
        var manager = new NodeManager(root);
        var channel = new NodeUpdateChannel(manager);
        var context = new UpdateContext(0.1, 10);

        root.Load();
        unloadedChild.Unload();
        channel.Update(context.DeltaTime);

        Assert.Single(root.Contexts);
        Assert.Single(child.Contexts);
        Assert.Empty(unloadedChild.Contexts);
        Assert.Equal(context.DeltaTime, root.Contexts[0].DeltaTime);
        Assert.Equal(context.DeltaTime, child.Contexts[0].DeltaTime);

        manager.Destroy();
    }

    [Fact]
    public void Constructor_PreservesChannelConfiguration()
    {
        var root = new Node("root");
        var manager = new NodeManager(root);
        var channel = new NodeUpdateChannel(
            manager,
            identifier: "scene-nodes",
            updateRate: 30,
            priority: UpdateChannelPriority.High
        );

        Assert.Equal("scene-nodes", channel.Identifier);
        Assert.Equal(30, channel.TargetUpdateRate);
        Assert.Equal(UpdateChannelPriority.High, channel.Priority);

        manager.Destroy();
    }

    private sealed class RecordingNode(string name, NodeOptions? options = null)
        : Node(name, options),
            IUpdatable
    {
        public List<UpdateContext> Contexts { get; } = [];

        public void Update(UpdateContext context)
        {
            Contexts.Add(context);
        }
    }
}
