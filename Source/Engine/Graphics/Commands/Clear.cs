using OpenGE.Graphics.Primitives;

namespace OpenGE.Graphics.Commands;

/// <summary>
/// Represents a command to clear the current rendering target.
/// </summary>
/// <param name="Color">The color to clear the target with.</param>
public readonly record struct ClearCommand(Color Color) : IRenderCommand;
