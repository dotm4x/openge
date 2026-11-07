using OpenGE.Common;
using OpenGE.Sandbox;
using OpenGE.Sandbox.Handling;
using OpenGE.Sandbox.Importing;
using OpenGE.Sandbox.Logging;
using OpenGE.Sandbox.Noding;
using OpenGE.Sandbox.Rendering;
using OpenGE.Sandbox.Scheduling;
using OpenGE.Sandbox.Windowing;

var engine = new MyGame();

try
{
    engine.Start();
}
finally
{
    engine.Stop();

    if (!engine.Destroyed)
        engine.Destroy();
}

namespace OpenGE.Sandbox
{
    internal sealed class MyGame : Engine
    {
        public readonly MyLogger Logger;
        public readonly MyWindowManager Windows;
        public readonly MyImporter Importer;
        public readonly MyInputHandler Input;
        public readonly MyNodeManager Nodes;
        public readonly MyRenderer Renderer;
        public readonly MyScheduler Scheduler;

        public MyGame()
        {
            Logger = new MyLogger();
            Windows = new MyWindowManager();
            Importer = new MyImporter();
            Input = new MyInputHandler(Windows.Main);
            Nodes = new MyNodeManager();
            Renderer = new MyRenderer(Nodes.Main.Camera, Windows.Main);
            Scheduler = new MyScheduler(Windows, Nodes, Renderer);
        }

        protected override IEnumerable<Module> Compose()
        {
            yield return Logger;
            yield return Windows;
            yield return Importer;
            yield return Input;
            yield return Nodes;
            yield return Renderer;
            yield return Scheduler;
        }

        protected override void OnStart()
        {
            Scheduler.Run();
        }
    }
}
