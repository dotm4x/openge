using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Noding;

namespace OpenGE.Rendering.Spaces;

/// <summary>
/// Initializes a new instance of the <see cref="NodeRenderSpace"/> class.
/// </summary>
/// <remarks>
/// Collects rendering commands from loaded renderable nodes under the active root.
///
/// The node manager and its nodes are borrowed and are never destroyed by this object.
/// Nodes are traversed in hierarchy order, and commands produced by each renderable
/// node preserve their original order.
/// </remarks>
/// <param name="manager">
/// The node manager providing the active root.
/// </param>
/// <param name="camera">
/// The initial camera used to render this space.
/// </param>
/// <param name="identifier">
/// The space identifier. The default is <c>"nodes"</c>.
/// </param>
/// <param name="priority">
/// The rendering priority. The default is
/// <see cref="RenderSpacePriority.Normal"/>.
/// </param>
public class NodeRenderSpace(
    NodeManager manager,
    ICamera camera,
    string identifier = "nodes",
    RenderSpacePriority priority = RenderSpacePriority.Normal
) : RenderSpace(identifier, camera, priority)
{
    private static IEnumerable<IRenderCommand> CollectNode(Node node, IRenderContext context)
    {
        if (node.Destroyed)
            yield break;

        if (node.Loaded && node is IRenderable renderable)
        {
            var data = renderable.Render(context);

            foreach (var command in data.Commands)
                yield return command;
        }

        foreach (var child in node.Subnodes)
        {
            foreach (var command in CollectNode(child, context))
                yield return command;
        }
    }

    /// <summary>
    /// Collects rendering commands from loaded renderable nodes under the active root.
    /// </summary>
    /// <param name="context">
    /// The rendering context used by nodes to produce their rendering data.
    /// </param>
    /// <returns>
    /// The rendering commands produced by the active node hierarchy, in traversal order.
    /// </returns>
    protected override IEnumerable<IRenderCommand> OnCollect(IRenderContext context)
    {
        return CollectNode(manager.ActiveRoot.Get(), context);
    }
}
