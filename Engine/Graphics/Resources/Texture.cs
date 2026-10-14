using OpenGE.Common;

namespace OpenGE.Graphics.Resources;

/// <summary>
/// Describes how a texture is sampled during rendering.
/// </summary>
/// <remarks>
/// A sampler controls texture filtering and the behavior of texture coordinates
/// outside the texture bounds.
///
/// Samplers are independent of textures, allowing the same texture to be
/// sampled using different filtering and addressing behavior.
/// </remarks>
public class TextureSampler(
    TextureFilter minFilter = TextureFilter.Linear,
    TextureFilter magFilter = TextureFilter.Linear,
    TextureAddressMode addressU = TextureAddressMode.ClampToEdge,
    TextureAddressMode addressV = TextureAddressMode.ClampToEdge
) : Resource
{
    /// <summary>
    /// Gets the addressing mode used for horizontal texture coordinates.
    /// </summary>
    public readonly TextureAddressMode AddressU = addressU;

    /// <summary>
    /// Gets the addressing mode used for vertical texture coordinates.
    /// </summary>
    public readonly TextureAddressMode AddressV = addressV;

    /// <summary>
    /// Gets the filtering mode used when the texture is magnified.
    /// </summary>
    public readonly TextureFilter MagFilter = magFilter;

    /// <summary>
    /// Gets the filtering mode used when the texture is minified.
    /// </summary>
    public readonly TextureFilter MinFilter = minFilter;
}

/// <summary>
/// Specifies how texture coordinates outside the normalized texture range
/// are resolved when sampling a texture.
/// </summary>
public enum TextureAddressMode
{
    /// <summary>
    /// Repeats the texture when coordinates extend beyond its bounds.
    /// </summary>
    Repeat,

    /// <summary>
    /// Repeats the texture while mirroring it on each repetition.
    /// </summary>
    MirroredRepeat,

    /// <summary>
    /// Clamps texture coordinates to the edge of the texture.
    /// </summary>
    ClampToEdge,
}

/// <summary>
/// Specifies how texels are filtered when sampling a texture.
/// </summary>
public enum TextureFilter
{
    /// <summary>
    /// Selects the nearest texel to the sampled texture coordinate.
    /// </summary>
    Nearest,

    /// <summary>
    /// Interpolates between neighboring texels around the sampled
    /// texture coordinate.
    /// </summary>
    Linear,
}

/// <summary>
/// Associates a texture with the sampler used to access it during rendering.
/// </summary>
/// <param name="Texture">
/// The texture to sample.
/// </param>
/// <param name="Sampler">
/// The sampler that defines how the texture is sampled.
/// </param>
/// <remarks>
/// The referenced texture and sampler are borrowed and are not owned by the
/// binding.
/// </remarks>
public readonly record struct TextureBinding(Texture Texture, TextureSampler Sampler);

/// <summary>
/// Initializes a new instance of the <see cref="Texture"/> class.
/// </summary>
/// <remarks>
/// <para>
/// Represents an image used as a texture for rendering.
/// </para>
/// The image contains the pixels in memory. Rendering backends create and manage
/// the corresponding native texture resource. The image is borrowed and is not
/// destroyed with the texture.
/// </remarks>
public class Texture(Image image) : Resource
{
    /// <summary>
    /// Gets the image used to create this texture.
    /// </summary>
    public readonly Image Image = image;

    /// <summary>
    /// Gets the texture height, in pixels.
    /// </summary>
    public uint Height => Image.Height;

    /// <summary>
    /// Gets the texture width, in pixels.
    /// </summary>
    public uint Width => Image.Width;
}
