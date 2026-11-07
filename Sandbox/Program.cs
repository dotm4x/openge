using System.Numerics;
using OpenGE.Common;
using OpenGE.Logging;
using OpenGE.Logging.Outputs;
using OpenGE.Noding;
using OpenGE.Spatial.Nodes;

var engine = new SandboxEngine();

try
{
    engine.Start();

    // Experiment here. The engine and its modules are running, and any
    // project referenced by this playground is available.
    engine
        .Get<Logger>()
        .From("Sandbox")
        .Log("Sandbox is running. Edit Sandbox/Program.cs to experiment.");

    engine.Stop();
}
finally
{
    engine.Destroy();
}

internal sealed class SandboxEngine : Engine
{
    public TModule Get<TModule>()
        where TModule : Module => Require<TModule>();

    protected override IEnumerable<Module> Compose()
    {
        yield return new Logger([new ConsoleOutput()]);
        yield return new NodeManager(new SandboxRoot("Root"));
    }

    protected override void Configure()
    {
        Require<Logger>().Log("Sandbox configured.", "Sandbox", LogMessageKind.Debug);
    }
}

internal sealed class SandboxRoot(string name) : Node(name)
{
    protected override IEnumerable<Node> Compose()
    {
        yield return new Node2D("Child") { Position = new Vector2(1, 2) };
    }
}
