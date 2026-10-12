using Mirage.Common;
using Mirage.Logging;
using Mirage.Logging.Outputs;
using Mirage.Noding;
using Mirage.Spatial.Nodes;

var engine = new SandboxEngine();
engine.Start();
engine.Stop();
engine.Destroy();

internal sealed class SandboxEngine : Engine
{
    protected override IEnumerable<Module> Compose()
    {
        yield return new Logger([new ConsoleOutput()]);
        yield return new NodeManager(new SandboxRoot());
    }
}

internal sealed class SandboxRoot() : Node("Main")
{
    protected override IEnumerable<Node> Compose()
    {
        yield return new Node2D("Player");
    }
}
