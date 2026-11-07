using OpenGE.Noding;
using OpenGE.Sandbox.Rendering;
using OpenGE.Scheduling;
using OpenGE.Scheduling.Channels;
using OpenGE.Windowing;

namespace OpenGE.Sandbox.Scheduling;

internal sealed class MyScheduler(WindowManager windows, NodeManager nodes, MyRenderer renderer)
    : Scheduler(targetUpdateRate: 0)
{
    public readonly UpdateChannel Process = new(
        identifier: "process",
        priority: UpdateChannelPriority.Critical,
        entries: [windows]
    );

    public readonly UpdateChannel Render = new(
        identifier: "render",
        priority: UpdateChannelPriority.Normal,
        entries: [renderer]
    );

    public readonly NodeUpdateChannel Nodes = new(nodes, "nodes");

    protected override IEnumerable<UpdateChannel> Compose()
    {
        yield return Process;
        yield return Render;
        yield return Nodes;
    }
}
