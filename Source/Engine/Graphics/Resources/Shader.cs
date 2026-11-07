using System.Text;
using OpenGE.Common;
using OpenGE.Importing;

namespace OpenGE.Graphics.Resources;

/// <summary>
/// Specifies the language of shader source code.
/// </summary>
public enum ShaderLanguage
{
    /// <summary>
    /// High-Level Shading Language.
    /// </summary>
    Hlsl,

    /// <summary>
    /// OpenGL Shading Language.
    /// </summary>
    Glsl,

    /// <summary>
    /// WebGPU Shading Language.
    /// </summary>
    Wgsl,

    /// <summary>
    /// Metal Shading Language.
    /// </summary>
    Msl,
}

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

    /// <summary>
    /// Executes general-purpose computations.
    /// </summary>
    Compute,
}

/// <summary>
/// Specifies the representation of compiled shader code.
/// </summary>
public enum ShaderFormat
{
    /// <summary>
    /// Standard Portable Intermediate Representation.
    /// </summary>
    SpirV,

    /// <summary>
    /// DirectX Intermediate Language.
    /// </summary>
    Dxil,
}

/// <summary>
/// Represents a shader stage supplied as source code or compiled code.
/// </summary>
/// <remarks>
/// A shader does not own a native graphics handle. Rendering backends create,
/// cache, and release their native representations separately.
/// </remarks>
public abstract class Shader : Resource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Shader"/> class.
    /// </summary>
    /// <param name="stage">The graphics pipeline stage.</param>
    /// <param name="entryPoint">The shader entry point.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The stage is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The entry point is empty.
    /// </exception>
    protected Shader(ShaderStage stage, string entryPoint = "main")
    {
        if (!Enum.IsDefined(stage))
            throw new ArgumentOutOfRangeException(nameof(stage));

        if (string.IsNullOrWhiteSpace(entryPoint))
            throw new ArgumentException("Shader entry point cannot be empty.", nameof(entryPoint));

        Stage = stage;
        EntryPoint = entryPoint;
    }

    /// <summary>
    /// Gets the graphics pipeline stage.
    /// </summary>
    public ShaderStage Stage { get; }

    /// <summary>
    /// Gets the shader entry point.
    /// </summary>
    public string EntryPoint { get; }
}

/// <summary>
/// Represents shader source code.
/// </summary>
public class ShaderSource : Shader
{
    private string _code;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderSource"/> class.
    /// </summary>
    /// <param name="code">The shader source code.</param>
    /// <param name="language">The source language.</param>
    /// <param name="stage">The graphics pipeline stage.</param>
    /// <param name="entryPoint">The shader entry point.</param>
    /// <param name="path">
    /// The optional source path used for diagnostics and include resolution.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The source code or entry point is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The language or stage is invalid.
    /// </exception>
    public ShaderSource(
        string code,
        ShaderLanguage language,
        ShaderStage stage,
        string entryPoint = "main",
        string? path = null
    )
        : base(stage, entryPoint)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Shader source code cannot be empty.", nameof(code));

        if (!Enum.IsDefined(language))
            throw new ArgumentOutOfRangeException(nameof(language));

        _code = code;

        Language = language;
        Path = path;
    }

    /// <summary>
    /// Gets the shader source code.
    /// </summary>
    public string Code
    {
        get
        {
            ThrowIfDestroyed();
            return _code;
        }
    }

    /// <summary>
    /// Gets the source language.
    /// </summary>
    public ShaderLanguage Language { get; }

    /// <summary>
    /// Gets the optional source path used for diagnostics and include resolution.
    /// </summary>
    public string? Path { get; }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _code = string.Empty;

        base.OnDestroy();
    }
}

/// <summary>
/// Represents compiled shader code.
/// </summary>
/// <remarks>
/// The format identifies the representation, but does not guarantee
/// compatibility with every device or rendering backend.
/// </remarks>
public class ShaderBinary : Shader
{
    private ReadOnlyMemory<byte> _code;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderBinary"/> class.
    /// </summary>
    /// <param name="code">The compiled shader code to copy.</param>
    /// <param name="stage">The graphics pipeline stage.</param>
    /// <param name="format">The compiled representation.</param>
    /// <param name="entryPoint">The shader entry point.</param>
    /// <exception cref="ArgumentException">
    /// The compiled code or entry point is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The format or stage is invalid.
    /// </exception>
    public ShaderBinary(
        ReadOnlySpan<byte> code,
        ShaderStage stage,
        ShaderFormat format,
        string entryPoint = "main"
    )
        : base(stage, entryPoint)
    {
        if (code.IsEmpty)
            throw new ArgumentException("Shader code cannot be empty.", nameof(code));

        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));

        _code = code.ToArray();

        Format = format;
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
    /// Gets the compiled representation.
    /// </summary>
    public ShaderFormat Format { get; }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _code = ReadOnlyMemory<byte>.Empty;

        base.OnDestroy();
    }
}

/// <summary>
/// Compiles shader source code into compiled shader resources.
/// </summary>
/// <remarks>
/// Compilation does not create a rendering backend's native shader program.
/// The caller owns the returned binary. The compiler does not own its input.
/// </remarks>
public abstract class ShaderCompiler
{
    /// <summary>
    /// Determines whether the specified compilation is supported.
    /// </summary>
    /// <param name="language">The source language.</param>
    /// <param name="stage">The graphics pipeline stage.</param>
    /// <param name="format">The compiled output format.</param>
    /// <returns>Whether the specified compilation is supported.</returns>
    public abstract bool Supports(ShaderLanguage language, ShaderStage stage, ShaderFormat format);

    /// <summary>
    /// Compiles shader source code into the requested format.
    /// </summary>
    /// <param name="source">The shader source code.</param>
    /// <param name="format">The compiled output format.</param>
    /// <returns>A new compiled shader resource.</returns>
    /// <exception cref="ArgumentNullException">
    /// The source is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The requested compilation is unsupported.
    /// </exception>
    public ShaderBinary Compile(ShaderSource source, ShaderFormat format)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!Supports(source.Language, source.Stage, format))
            throw new NotSupportedException(
                $"Compilation of '{source.Language}' "
                    + $"stage '{source.Stage}' to '{format}' is not supported."
            );

        // Accessing Code also checks the source resource's lifecycle.
        _ = source.Code;

        return OnCompile(source, format);
    }

    /// <summary>
    /// Compiles a supported source into the requested format.
    /// </summary>
    /// <param name="source">The shader source code.</param>
    /// <param name="format">The compiled output format.</param>
    /// <returns>A new compiled shader resource.</returns>
    protected abstract ShaderBinary OnCompile(ShaderSource source, ShaderFormat format);
}
