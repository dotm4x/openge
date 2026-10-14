using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;

namespace OpenGE.Graphics;

/// <summary>
/// Represents the ordered rendering commands produced by a renderable object.
/// </summary>
/// <remarks>
/// Rendering data describes the graphical operations required to represent an
/// object during a rendering frame.
///
/// Commands are stored and later executed by the rendering system in the order
/// in which they appear in <see cref="Commands"/>. Rendering data does not
/// execute commands or depend on a specific rendering backend.
/// </remarks>
/// <param name="commands">
/// The rendering commands to include, in execution order.
/// </param>
public class RenderData(IEnumerable<IRenderCommand> commands)
{
    /// <summary>
    /// Gets the rendering commands produced for this rendering data, in execution order.
    /// </summary>
    public IReadOnlyList<IRenderCommand> Commands { get; } = [.. commands];
}
