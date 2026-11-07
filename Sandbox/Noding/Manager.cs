using OpenGE.Noding;

namespace OpenGE.Sandbox.Noding;

internal sealed class MyNodeManager : NodeManager
{
    public readonly MyRoot Main;

    public MyNodeManager()
        : this(new MyRoot()) { }

    private MyNodeManager(MyRoot main)
        : base(main, dependencies: ["Importer", "InputHandler-0"])
    {
        Main = main;
    }
}
