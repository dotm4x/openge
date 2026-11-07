using OpenGE.Graphics.Nodes;
using OpenGE.Noding;

namespace OpenGE.Sandbox.Noding;

internal sealed class MyRoot() : Node("Root")
{
    public Camera2D Camera { get; } = new();

    protected override IEnumerable<Node> Compose()
    {
        yield return Camera;
    }
}
