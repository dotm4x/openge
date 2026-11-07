using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Windowing;

internal static class VideoRuntime
{
    private static readonly Lock Gate = new();
    private static int _leases;
    private static bool _ownsVideo;

    public static void Acquire()
    {
        lock (Gate)
        {
            if (_leases > 0)
            {
                _leases++;
                return;
            }
            _ownsVideo = !SDL.WasInit(SDL.InitFlags.Video).HasFlag(SDL.InitFlags.Video);
            if (_ownsVideo && !SDL.InitSubSystem(SDL.InitFlags.Video))
            {
                _ownsVideo = false;
                throw new InvalidOperationException(
                    $"Initializing SDL video failed: {SDL.GetError()}"
                );
            }
            _leases = 1;
        }
    }

    public static void Release()
    {
        lock (Gate)
        {
            if (_leases == 0 || --_leases != 0)
                return;
            if (_ownsVideo)
                SDL.QuitSubSystem(SDL.InitFlags.Video);
            _ownsVideo = false;
        }
    }
}
