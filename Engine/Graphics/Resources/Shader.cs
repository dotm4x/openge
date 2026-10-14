using OpenGE.Common;

namespace OpenGE.Graphics.Resources;

/// <summary>
/// Specifies the stage in the graphics pipeline at which a shader executes.
/// </summary>
public enum ShaderStage
{
    /// <summary>
    /// Processes individual vertices.
    /// </summary>
    Vertex,

    /// <summary>
    /// Processes fragments produced by rasterized geometry.
    /// </summary>
    Fragment,
}

/// <summary>
/// Specifies the compiled representation of a shader.
/// </summary>
public enum ShaderFormat
{
    /// <summary>
    /// Standard Portable Intermediate Representation for Vulkan.
    /// </summary>
    SpirV,
}

/// <summary>
/// Represents compiled shader code that can be consumed by a rendering backend.
/// </summary>
/// <remarks>
/// A shader contains backend-independent metadata together with compiled code
/// in a specific format. The rendering backend is responsible for determining
/// whether the format is supported and creating the corresponding native
/// shader resource.
/// </remarks>
public sealed class Shader : Resource
{
    private ReadOnlyMemory<byte> _code;

    /// <summary>
    /// Initializes a new instance of the <see cref="Shader"/> class.
    /// </summary>
    /// <param name="code">The compiled shader code to copy.</param>
    /// <param name="stage">The graphics pipeline stage of the shader.</param>
    /// <param name="format">The format of the compiled shader code.</param>
    /// <param name="entryPoint">The shader entry point.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the shader code is empty or the entry point is empty.
    /// </exception>
    public Shader(
        ReadOnlySpan<byte> code,
        ShaderStage stage,
        ShaderFormat format,
        string entryPoint = "main"
    )
    {
        if (code.IsEmpty)
            throw new ArgumentException("Shader code cannot be empty.", nameof(code));

        if (string.IsNullOrWhiteSpace(entryPoint))
            throw new ArgumentException("Shader entry point cannot be empty.", nameof(entryPoint));

        _code = code.ToArray();

        Stage = stage;
        Format = format;
        EntryPoint = entryPoint;
    }

    /// <summary>
    /// Gets the compiled shader code.
    /// </summary>
    public ReadOnlyMemory<byte> Code
    {
        get
        {
            ThrowIfDestroyed();
            return _code;
        }
    }

    /// <summary>
    /// Gets the shader entry point.
    /// </summary>
    public string EntryPoint { get; }

    /// <summary>
    /// Gets the format of the compiled shader code.
    /// </summary>
    public ShaderFormat Format { get; }

    /// <summary>
    /// Gets the graphics pipeline stage of the shader.
    /// </summary>
    public ShaderStage Stage { get; }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _code = ReadOnlyMemory<byte>.Empty;

        base.OnDestroy();
    }
}
