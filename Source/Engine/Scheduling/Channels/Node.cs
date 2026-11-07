using OpenGE.Noding;
using OpenGE.Scheduling.Interfaces;

namespace OpenGE.Scheduling.Channels;

/// <summary>
/// Initializes a new instance of the <see cref="NodeUpdateChannel"/> class.
/// </summary>
/// <remarks>
/// Updates loaded <see cref="IUpdatable"/> nodes in the graph's active root.
/// The node manager and its nodes are borrowed and are never destroyed by this object.
/// </remarks>
/// <param name="manager">The node manager providing the active root.</param>
/// <param name="identifier">The channel identifier. The default is <c>"nodes"</c>.</param>
/// <param name="updateRate"> The target number of updates per second. A non-positive value updates once per scheduler iteration. </param>
/// <param name="priority">The update priority. The default is <see cref="UpdateChannelPriority.Normal"/>.</param>
public class NodeUpdateChannel(
    NodeManager manager,
    string identifier = "nodes",
    double updateRate = 0,
    UpdateChannelPriority priority = UpdateChannelPriority.Normal
) : UpdateChannel(identifier, updateRate, priority)
{
    private readonly NodeManager _graph = manager;

    private static void Visit(Node node, UpdateContext context)
    {
        if (node.Destroyed)
            return;

        if (node.Loaded && node is IUpdatable updatable)
            updatable.Update(context);

        if (node.Destroyed)
            return;

        foreach (var child in node.Subnodes.ToArray())
            Visit(child, context);
    }

    /// <inheritdoc />
    protected override void OnUpdate(UpdateContext context)
    {
        Visit(_graph.ActiveRoot.Get(), context);
    }
}
