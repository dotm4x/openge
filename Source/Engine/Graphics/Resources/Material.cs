using OpenGE.Common;
using OpenGE.Graphics.Resources.Textures;

namespace OpenGE.Graphics.Resources;

/// <summary>
/// Represents the graphical resources and state used to render geometry.
/// </summary>
/// <remarks>
/// A material defines how geometry is processed by the graphics pipeline
/// using vertex and fragment shaders, together with the textures and samplers
/// consumed by those shaders.
///
/// All referenced shaders, textures, and samplers are borrowed and are not
/// destroyed with the material.
/// </remarks>
public class GraphicMaterial : Resource
{
    /// <summary>
    /// Gets the fragment shader used to process rasterized fragments.
    /// </summary>
    /// <remarks>
    /// The shader is borrowed and is not destroyed with the material.
    /// </remarks>
    public readonly Shader FragmentShader;

    /// <summary>
    /// Gets the vertex shader used to process geometry vertices.
    /// </summary>
    /// <remarks>
    /// The shader is borrowed and is not destroyed with the material.
    /// </remarks>
    public readonly Shader VertexShader;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicMaterial"/> class.
    /// </summary>
    /// <param name="vertexShader">
    /// The vertex shader used to process geometry vertices.
    /// </param>
    /// <param name="fragmentShader">
    /// The fragment shader used to process rasterized fragments.
    /// </param>
    /// <param name="textures">
    /// The texture and sampler bindings consumed by the material,
    /// or <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="vertexShader"/> is not a vertex shader or
    /// <paramref name="fragmentShader"/> is not a fragment shader.
    /// </exception>
    public GraphicMaterial(
        Shader vertexShader,
        Shader fragmentShader,
        IEnumerable<TextureBinding>? textures = null
    )
    {
        if (vertexShader.Stage != ShaderStage.Vertex)
        {
            throw new ArgumentException(
                "The vertex shader must use the vertex stage.",
                nameof(vertexShader)
            );
        }

        if (fragmentShader.Stage != ShaderStage.Fragment)
        {
            throw new ArgumentException(
                "The fragment shader must use the fragment stage.",
                nameof(fragmentShader)
            );
        }

        VertexShader = vertexShader;
        FragmentShader = fragmentShader;
        Textures = textures?.ToArray() ?? [];
    }

    /// <summary>
    /// Gets the texture and sampler bindings consumed by the material.
    /// </summary>
    /// <remarks>
    /// The referenced textures and samplers are borrowed and are not destroyed
    /// with the material.
    /// </remarks>
    public IReadOnlyList<TextureBinding> Textures { get; }
}
