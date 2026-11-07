using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Resources.Targets;
using OpenGE.Noding;

namespace OpenGE.Rendering.Spaces;

/// <summary>
/// Collects rendering commands from the loaded, visible nodes under the active root.
/// </summary>
/// <remarks>
/// The node manager and its nodes are borrowed and are never destroyed by this object.
/// Nodes are traversed in hierarchy order, and commands produced by each renderable
/// node preserve their original order.
///
/// Destroyed and invisible nodes are skipped together with their subtrees.
/// A snapshot of the subnodes is taken before each subtree is traversed, so a
/// renderable may modify the hierarchy while producing its commands.
///
/// The rendering target is borrowed. When no target is specified, commands
/// are rendered to the window associated with the renderer.
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
/// <param name="target">
/// The initial offscreen rendering target, or <see langword="null"/> to render
/// to the window.
/// </param>
/// <param name="priority">
/// The rendering priority. The default is
/// <see cref="RenderSpacePriority.Normal"/>.
/// </param>
public class NodeRenderSpace(
    NodeManager manager,
    ICamera camera,
    string identifier = "nodes",
    RenderTarget? target = null,
    RenderSpacePriority priority = RenderSpacePriority.Normal
) : RenderSpace(identifier, camera, target, priority)
{
    private static IEnumerable<IRenderCommand> CollectNode(Node node, IRenderContext context)
    {
        if (node.Destroyed || !node.Visible.Value)
            yield break;

        if (node.Loaded && node is IRenderable renderable)
        {
            var data = renderable.Render(context);

            foreach (var command in data.Commands)
                yield return command;
        }

        foreach (var child in node.Subnodes.ToArray())
        {
            foreach (var command in CollectNode(child, context))
                yield return command;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Collects the rendering commands of the loaded, visible nodes under the active
    /// root, in traversal order.
    /// </remarks>
    protected override IEnumerable<IRenderCommand> OnCollect(IRenderContext context)
    {
        return CollectNode(manager.ActiveRoot.Value, context);
    }
}
