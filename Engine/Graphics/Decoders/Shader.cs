using System.Text;
using OpenGE.Graphics.Resources;
using OpenGE.Importing;

namespace OpenGE.Graphics.Decoders;

/// <summary>
/// Decodes shader source files, optionally compiling them.
/// </summary>
/// <remarks>
/// The source language is configured explicitly. Vertex, fragment, and compute
/// shaders use the .vert, .frag, and .comp extensions respectively.
/// The decoder does not own its compiler.
/// </remarks>
public class ShaderDecoder : Decoder<Shader>
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vert",
        ".frag",
        ".comp",
    };

    /// <summary>
    /// Initializes a decoder that returns shader source resources.
    /// </summary>
    /// <param name="language">The source language.</param>
    /// <param name="entryPoint">The shader entry point.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The language is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The entry point is empty or contains a null character.
    /// </exception>
    public ShaderDecoder(ShaderLanguage language, string entryPoint = "main")
    {
        if (!Enum.IsDefined(language))
            throw new ArgumentOutOfRangeException(nameof(language));

        if (string.IsNullOrWhiteSpace(entryPoint) || entryPoint.Contains('\0'))
            throw new ArgumentException(
                "The entry point cannot be empty or contain null characters.",
                nameof(entryPoint)
            );

        Language = language;
        EntryPoint = entryPoint;
    }

    /// <summary>
    /// Initializes a decoder that compiles shader source files.
    /// </summary>
    /// <param name="language">The source language.</param>
    /// <param name="compiler">The compiler to use.</param>
    /// <param name="target">The compiled output format.</param>
    /// <param name="entryPoint">The shader entry point.</param>
    /// <exception cref="ArgumentNullException">
    /// The compiler is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The language or target format is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The entry point is empty or contains a null character.
    /// </exception>
    public ShaderDecoder(
        ShaderLanguage language,
        ShaderCompiler compiler,
        ShaderFormat target,
        string entryPoint = "main"
    )
        : this(language, entryPoint)
    {
        ArgumentNullException.ThrowIfNull(compiler);

        if (!Enum.IsDefined(target))
            throw new ArgumentOutOfRangeException(nameof(target));

        Compiler = compiler;
        Target = target;
    }

    /// <summary>
    /// Gets the source language.
    /// </summary>
    public ShaderLanguage Language { get; }

    /// <summary>
    /// Gets the shader entry point.
    /// </summary>
    public string EntryPoint { get; }

    /// <summary>
    /// Gets the optional compiler.
    /// </summary>
    public ShaderCompiler? Compiler { get; }

    /// <summary>
    /// Gets the compiled output format, or <see langword="null"/>
    /// when the decoder returns source code.
    /// </summary>
    public ShaderFormat? Target { get; }

    private static ShaderStage GetStage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".vert" => ShaderStage.Vertex,
            ".frag" => ShaderStage.Fragment,
            ".comp" => ShaderStage.Compute,
            var extension => throw new NotSupportedException(
                $"Shader extension '{extension}' is not supported."
            ),
        };
    }

    /// <inheritdoc />
    protected override Shader OnDecode(ImportContext context, Stream stream)
    {
        var stage = GetStage(context.Path);

        if (Compiler is not null && !Compiler.Supports(Language, stage, Target!.Value))
            throw new NotSupportedException(
                $"Compilation of '{Language}' " + $"stage '{stage}' to '{Target}' is not supported."
            );

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true
        );

        var source = new ShaderSource(
            reader.ReadToEnd(),
            Language,
            stage,
            EntryPoint,
            context.Path
        );

        if (Compiler is null)
            return source;

        try
        {
            return Compiler.Compile(source, Target!.Value);
        }
        finally
        {
            source.Destroy();
        }
    }

    /// <inheritdoc />
    public override bool Supports(string extension)
    {
        return Extensions.Contains(extension);
    }
}
