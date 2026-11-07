namespace OpenGE.Graphics.Interfaces;

/// <summary>
/// Defines an object that can produce rendering data during a rendering frame.
/// </summary>
/// <remarks>
/// The rendering system calls <see cref="Render"/> to obtain the ordered
/// rendering commands required to represent the object in the current frame.
///
/// Renderable objects describe their graphical representation without executing
/// rendering operations or depending on a specific rendering backend.
/// </remarks>
public interface IRenderable
{
    /// <summary>
    /// Produces the rendering data for this object using the specified rendering context.
    /// </summary>
    /// <param name="context">
    /// The context that provides the rendering state available for the current frame.
    /// </param>
    /// <returns>
    /// The rendering data containing the commands required to represent this object.
    /// </returns>
    RenderData Render(IRenderContext context);
}
