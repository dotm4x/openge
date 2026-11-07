using OpenGE.Common;

namespace OpenGE.Graphics.Resources.Textures;

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
