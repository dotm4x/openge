namespace OpenGE.Graphics.Resources.Textures;

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
