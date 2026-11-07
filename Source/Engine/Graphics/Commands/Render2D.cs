using System.Numerics;
using OpenGE.Graphics.Geometry;
using OpenGE.Graphics.Resources;

namespace OpenGE.Graphics.Commands;

/// <summary>
/// Represents a command for rendering two-dimensional geometry.
/// </summary>
/// <param name="Mesh">
/// The graphical mesh containing the geometry to render.
/// </param>
/// <param name="Material">
/// The material defining how the geometry is rendered.
/// </param>
/// <param name="Transform">
/// The transformation applied to the geometry.
/// </param>
/// <remarks>
/// The command describes a backend-independent two-dimensional rendering
/// operation. Rendering backends are responsible for translating the mesh,
/// material, and transformation into their native graphics operations.
///
/// Backends combine <paramref name="Transform"/> with the view and projection of
/// the rendering context in this order: the two-dimensional transform is promoted
/// to a four-dimensional matrix that is applied first, followed by the camera view
/// and then the projection produced for the current viewport.
///
/// The referenced resources are borrowed and are not owned by the command.
/// </remarks>
public readonly record struct Render2DCommand(
    GraphicMesh2D Mesh,
    GraphicMaterial Material,
    Matrix3x2 Transform
) : IRenderCommand;
