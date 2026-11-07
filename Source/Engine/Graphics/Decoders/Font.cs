using OpenGE.Graphics.Resources;
using OpenGE.Importing;

namespace OpenGE.Graphics.Decoders;

/// <summary>
/// Decodes supported font files into reusable font data.
/// </summary>
public class FontDecoder : Decoder<Font>
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
    };

    /// <inheritdoc />
    protected override Font OnDecode(ImportContext context, Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return new Font(buffer.ToArray());
    }

    /// <inheritdoc />
    public override bool Supports(string extension)
    {
        return Extensions.Contains(extension);
    }
}
