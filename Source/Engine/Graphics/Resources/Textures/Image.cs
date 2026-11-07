using System.Numerics;

namespace OpenGE.Graphics.Resources.Textures;

/// <summary>
/// Represents a texture whose contents originate from an image.
/// </summary>
/// <remarks>
/// The source image is borrowed and is not destroyed with the texture.
/// </remarks>
public class ImageTexture(Image image) : Texture
{
    /// <summary>
    /// Gets the source image.
    /// </summary>
    public Image Image { get; } = image ?? throw new ArgumentNullException(nameof(image));

    /// <inheritdoc />
    public override Vector2 Size => new(Image.Width, Image.Height);
}
