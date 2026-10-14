namespace OpenGE.Graphics.Resources.Textures;

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
