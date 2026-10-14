using System.Text;
using OpenGE.Graphics.Resources;
using OpenGE.Importing;
using Vortice.Dxc;

namespace OpenGE.Graphics.Decoders;

/// <summary>
/// Decodes HLSL shader files into compiled <see cref="Shader"/> resources.
/// </summary>
/// <remarks>
/// Vertex shaders use the <c>.vert</c> extension and fragment shaders use
/// the <c>.frag</c> extension. Shader source is compiled for the configured
/// target format.
/// </remarks>
public class ShaderDecoder : Decoder<Shader>
{
    private const string EntryPoint = "main";

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vert",
        ".frag",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderDecoder"/> class.
    /// </summary>
    /// <param name="target">
    /// The compiled shader format produced by the decoder.
    /// </param>
    public ShaderDecoder(ShaderFormat target = ShaderFormat.SpirV)
    {
        Target = target;
    }

    /// <summary>
    /// Gets the compiled shader format produced by the decoder.
    /// </summary>
    public ShaderFormat Target { get; }

    private static byte[] Compile(
        string source,
        ShaderStage stage,
        string path,
        DxcCompilerOptions options
    )
    {
        var dxcStage = stage switch
        {
            ShaderStage.Vertex => DxcShaderStage.Vertex,
            ShaderStage.Fragment => DxcShaderStage.Pixel,
            _ => throw new NotSupportedException($"Shader stage '{stage}' is not supported."),
        };

        using var result = DxcCompiler.Compile(dxcStage, source, EntryPoint, options, path);

        if (result.GetStatus() >= 0)
            return result.GetObjectBytecodeArray();
        var errors = result.GetErrors();

        throw new InvalidDataException(
            string.IsNullOrWhiteSpace(errors) ? $"Failed to compile shader '{path}'." : errors
        );
    }

    private static byte[] CompileToSpirV(string source, ShaderStage stage, string path)
    {
        var options = new DxcCompilerOptions { GenerateSpirv = true };

        return Compile(source, stage, path, options);
    }

    private static ShaderStage GetStage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".vert" => ShaderStage.Vertex,
            ".frag" => ShaderStage.Fragment,
            var extension => throw new NotSupportedException(
                $"Shader extension '{extension}' is not supported."
            ),
        };
    }

    /// <inheritdoc />
    protected override Shader OnDecode(ImportContext context, Stream stream)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true
        );

        var source = reader.ReadToEnd();
        var stage = GetStage(context.Path);

        var code = Target switch
        {
            ShaderFormat.SpirV => CompileToSpirV(source, stage, context.Path),
            _ => throw new NotSupportedException(
                $"Shader format '{Target}' is not supported by this decoder."
            ),
        };

        return new Shader(code, stage, Target, EntryPoint);
    }

    /// <inheritdoc />
    public override bool Supports(string extension)
    {
        return Extensions.Contains(extension);
    }
}
