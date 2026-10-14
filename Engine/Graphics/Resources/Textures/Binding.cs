namespace OpenGE.Graphics.Resources.Textures;

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
