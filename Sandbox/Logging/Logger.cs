using OpenGE.Logging;
using OpenGE.Logging.Outputs;

namespace OpenGE.Sandbox.Logging;

internal sealed class MyLogger : Logger
{
    protected override IEnumerable<LogOutput> Compose()
    {
        yield return new ConsoleOutput();
    }
}
