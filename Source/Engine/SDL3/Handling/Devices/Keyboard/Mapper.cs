using OpenGE.Handling.Devices.Keyboard;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Handling.Devices.Keyboard;

/// <summary>
/// Converts SDL physical scancodes to OpenGE keyboard keys.
/// </summary>
public static class SDL3KeyboardKeyMapper
{
    /// <summary>
    /// Maps an SDL physical scancode to the corresponding OpenGE key.
    /// </summary>
    /// <param name="scancode">The SDL scancode to map.</param>
    /// <returns>
    /// The matching key, or <see cref="KeyboardKey.Unknown"/> when no mapping exists.
    /// </returns>
    public static KeyboardKey FromScancode(SDL.Scancode scancode)
    {
        var code = (int)scancode;

        return code switch
        {
            // SDL scancodes A–Z are consecutive.
            >= 4 and <= 29 => (KeyboardKey)((int)KeyboardKey.A + code - 4),
            // SDL scancodes 1–9 are consecutive; 0 comes afterward.
            >= 30 and <= 38 => (KeyboardKey)((int)KeyboardKey.Digit1 + code - 30),
            // SDL scancodes F1–F12 and F13–F24 are consecutive.
            >= 58 and <= 69 => (KeyboardKey)((int)KeyboardKey.F1 + code - 58),
            >= 104 and <= 115 => (KeyboardKey)((int)KeyboardKey.F13 + code - 104),
            // Numeric keypad digits 1–9 are consecutive.
            >= 89 and <= 97 => (KeyboardKey)((int)KeyboardKey.Numpad1 + code - 89),
            _ => code switch
            {
                39 => KeyboardKey.Digit0,

                40 => KeyboardKey.Enter,
                41 => KeyboardKey.Escape,
                42 => KeyboardKey.Backspace,
                43 => KeyboardKey.Tab,
                44 => KeyboardKey.Space,
                45 => KeyboardKey.Minus,
                46 => KeyboardKey.Equal,
                47 => KeyboardKey.LeftBracket,
                48 => KeyboardKey.RightBracket,
                49 => KeyboardKey.Backslash,
                50 => KeyboardKey.NonUsHash,
                51 => KeyboardKey.Semicolon,
                52 => KeyboardKey.Apostrophe,
                53 => KeyboardKey.Grave,
                54 => KeyboardKey.Comma,
                55 => KeyboardKey.Period,
                56 => KeyboardKey.Slash,
                57 => KeyboardKey.CapsLock,

                70 => KeyboardKey.PrintScreen,
                71 => KeyboardKey.ScrollLock,
                72 => KeyboardKey.Pause,
                73 => KeyboardKey.Insert,
                74 => KeyboardKey.Home,
                75 => KeyboardKey.PageUp,
                76 => KeyboardKey.Delete,
                77 => KeyboardKey.End,
                78 => KeyboardKey.PageDown,
                79 => KeyboardKey.ArrowRight,
                80 => KeyboardKey.ArrowLeft,
                81 => KeyboardKey.ArrowDown,
                82 => KeyboardKey.ArrowUp,

                83 => KeyboardKey.NumLock,
                84 => KeyboardKey.NumpadDivide,
                85 => KeyboardKey.NumpadMultiply,
                86 => KeyboardKey.NumpadSubtract,
                87 => KeyboardKey.NumpadAdd,
                88 => KeyboardKey.NumpadEnter,
                98 => KeyboardKey.Numpad0,
                99 => KeyboardKey.NumpadDecimal,
                100 => KeyboardKey.NonUsBackslash,
                101 => KeyboardKey.Menu,
                103 => KeyboardKey.NumpadEqual,

                127 => KeyboardKey.VolumeMute,
                128 => KeyboardKey.VolumeUp,
                129 => KeyboardKey.VolumeDown,

                224 => KeyboardKey.LeftControl,
                225 => KeyboardKey.LeftShift,
                226 => KeyboardKey.LeftAlt,
                227 => KeyboardKey.LeftSuper,
                228 => KeyboardKey.RightControl,
                229 => KeyboardKey.RightShift,
                230 => KeyboardKey.RightAlt,
                231 => KeyboardKey.RightSuper,

                267 => KeyboardKey.MediaNextTrack,
                268 => KeyboardKey.MediaPreviousTrack,
                269 => KeyboardKey.MediaStop,
                271 => KeyboardKey.MediaPlayPause,

                _ => KeyboardKey.Unknown,
            },
        };
    }
}
