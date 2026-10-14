using System.Globalization;

namespace OpenGE.Graphics.Primitives;

/// <summary>
/// Represents a color using normalized red, green, blue, and alpha components.
/// </summary>
/// <remarks>
/// Color components commonly range from <c>0</c> to <c>1</c>, although values
/// outside that range are permitted for operations such as HDR rendering.
///
/// Hexadecimal colors may use the <c>#RGB</c>, <c>#RGBA</c>,
/// <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> formats.
/// </remarks>
public readonly record struct Color
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Color"/> struct.
    /// </summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    /// <param name="alpha">The alpha component.</param>
    /// <param name="hexadecimal">
    /// An optional hexadecimal representation of the color. When provided,
    /// this value takes precedence and the component arguments are ignored.
    /// </param>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="hexadecimal"/> is not a valid color.
    /// </exception>
    public Color(
        float red = 0.0f,
        float green = 0.0f,
        float blue = 0.0f,
        float alpha = 1.0f,
        string? hexadecimal = null
    )
    {
        if (!string.IsNullOrWhiteSpace(hexadecimal))
        {
            this = Parse(hexadecimal);
            return;
        }

        R = red;
        G = green;
        B = blue;
        A = alpha;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Color"/> struct.
    /// </summary>
    /// <param name="hexadecimal">
    /// A hexadecimal color in <c>#RGB</c>, <c>#RGBA</c>,
    /// <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> format.
    /// </param>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="hexadecimal"/> is not a valid color.
    /// </exception>
    public Color(string hexadecimal)
    {
        this = Parse(hexadecimal);
    }

    /// <summary>
    /// Gets the alpha component.
    /// </summary>
    public float A { get; }

    /// <summary>
    /// Gets the blue component.
    /// </summary>
    public float B { get; }

    /// <summary>
    /// Gets the color black.
    /// </summary>
    public static Color Black => new(0.0f);

    /// <summary>
    /// Gets the color blue.
    /// </summary>
    public static Color Blue => new(0.0f, 0.0f, 1.0f);

    /// <summary>
    /// Gets cornflower blue.
    /// </summary>
    public static Color CornflowerBlue => FromHex("#6495ED");

    /// <summary>
    /// Gets the color cyan.
    /// </summary>
    public static Color Cyan => new(0.0f, 1.0f, 1.0f);

    /// <summary>
    /// Gets the green component.
    /// </summary>
    public float G { get; }

    /// <summary>
    /// Gets the color gray.
    /// </summary>
    public static Color Gray => new(0.5f, 0.5f, 0.5f);

    /// <summary>
    /// Gets the color green.
    /// </summary>
    public static Color Green => new(0.0f, 1.0f);

    /// <summary>
    /// Gets the color magenta.
    /// </summary>
    public static Color Magenta => new(1.0f, 0.0f, 1.0f);

    /// <summary>
    /// Gets the red component.
    /// </summary>
    public float R { get; }

    /// <summary>
    /// Gets the color red.
    /// </summary>
    public static Color Red => new(1.0f);

    /// <summary>
    /// Gets a fully transparent color.
    /// </summary>
    public static Color Transparent => new(0.0f, 0.0f, 0.0f, 0.0f);

    /// <summary>
    /// Gets the color white.
    /// </summary>
    public static Color White => new(1.0f, 1.0f, 1.0f);

    /// <summary>
    /// Gets the color yellow.
    /// </summary>
    public static Color Yellow => new(1.0f, 1.0f);

    private static byte ExpandNibble(byte value)
    {
        return (byte)((value << 4) | value);
    }

    private static byte ToByte(float value)
    {
        return (byte)MathF.Round(System.Math.Clamp(value, 0.0f, 1.0f) * byte.MaxValue);
    }

    private static bool TryParseByte(ReadOnlySpan<char> value, out byte result)
    {
        return byte.TryParse(
            value,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out result
        );
    }

    private static bool TryParseLong(ReadOnlySpan<char> hexadecimal, bool hasAlpha, out Color color)
    {
        color = default;

        if (
            !TryParseByte(hexadecimal[..2], out var red)
            || !TryParseByte(hexadecimal.Slice(2, 2), out var green)
            || !TryParseByte(hexadecimal.Slice(4, 2), out var blue)
        )
        {
            return false;
        }

        var alpha = byte.MaxValue;

        if (hasAlpha && !TryParseByte(hexadecimal.Slice(6, 2), out alpha))
        {
            return false;
        }

        color = FromBytes(red, green, blue, alpha);
        return true;
    }

    private static bool TryParseNibble(char value, out byte result)
    {
        var parsed = byte.TryParse(
            value.ToString(),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out result
        );

        return parsed;
    }

    private static bool TryParseShort(
        ReadOnlySpan<char> hexadecimal,
        bool hasAlpha,
        out Color color
    )
    {
        color = default;

        if (
            !TryParseNibble(hexadecimal[0], out var red)
            || !TryParseNibble(hexadecimal[1], out var green)
            || !TryParseNibble(hexadecimal[2], out var blue)
        )
        {
            return false;
        }

        byte alpha = byte.MaxValue;

        if (hasAlpha && !TryParseNibble(hexadecimal[3], out alpha))
        {
            return false;
        }

        color = FromBytes(
            ExpandNibble(red),
            ExpandNibble(green),
            ExpandNibble(blue),
            ExpandNibble(alpha)
        );

        return true;
    }

    /// <summary>
    /// Constrains every component of a color to the specified range.
    /// </summary>
    /// <param name="color">The color to constrain.</param>
    /// <param name="minimum">The minimum component value.</param>
    /// <param name="maximum">The maximum component value.</param>
    /// <returns>The constrained color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimum"/> is greater than
    /// <paramref name="maximum"/>.
    /// </exception>
    public static Color Clamp(Color color, float minimum = 0.0f, float maximum = 1.0f)
    {
        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                "The minimum cannot be greater than the maximum."
            );
        }

        return new Color(
            System.Math.Clamp(color.R, minimum, maximum),
            System.Math.Clamp(color.G, minimum, maximum),
            System.Math.Clamp(color.B, minimum, maximum),
            System.Math.Clamp(color.A, minimum, maximum)
        );
    }

    /// <summary>
    /// Returns this color with every component constrained to the normalized
    /// range from 0 to 1.
    /// </summary>
    /// <returns>The clamped color.</returns>
    public Color Clamped()
    {
        return Clamp(this);
    }

    /// <summary>
    /// Creates a color from 8-bit component values.
    /// </summary>
    /// <param name="red">The red component, ranging from 0 to 255.</param>
    /// <param name="green">The green component, ranging from 0 to 255.</param>
    /// <param name="blue">The blue component, ranging from 0 to 255.</param>
    /// <param name="alpha">The alpha component, ranging from 0 to 255.</param>
    /// <returns>A color with normalized components.</returns>
    public static Color FromBytes(byte red, byte green, byte blue, byte alpha = byte.MaxValue)
    {
        const float scale = 1.0f / byte.MaxValue;

        return new Color(red * scale, green * scale, blue * scale, alpha * scale);
    }

    /// <summary>
    /// Creates a color from a hexadecimal value.
    /// </summary>
    /// <param name="hexadecimal">
    /// A hexadecimal color in <c>#RGB</c>, <c>#RGBA</c>,
    /// <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> format.
    /// </param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="hexadecimal"/> is not a valid color.
    /// </exception>
    public static Color FromHex(string hexadecimal)
    {
        return Parse(hexadecimal);
    }

    /// <summary>
    /// Linearly interpolates between two colors.
    /// </summary>
    /// <param name="start">The starting color.</param>
    /// <param name="end">The ending color.</param>
    /// <param name="amount">
    /// The interpolation amount, where 0 returns <paramref name="start"/> and
    /// 1 returns <paramref name="end"/>.
    /// </param>
    /// <returns>The interpolated color.</returns>
    public static Color Lerp(Color start, Color end, float amount)
    {
        return start + ((end - start) * amount);
    }

    /// <summary>
    /// Parses a hexadecimal color.
    /// </summary>
    /// <param name="value">
    /// A hexadecimal color in <c>#RGB</c>, <c>#RGBA</c>,
    /// <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> format.
    /// </param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="value"/> is not a valid color.
    /// </exception>
    public static Color Parse(string value)
    {
        return !TryParse(value, out var color)
            ? throw new FormatException($"'{value}' is not a valid hexadecimal color.")
            : color;
    }

    /// <summary>
    /// Creates a random opaque color with normalized red, green, and blue components.
    /// </summary>
    /// <returns>A random color with an alpha component of 1.</returns>
    public static Color Random()
    {
        return new Color(
            System.Random.Shared.NextSingle(),
            System.Random.Shared.NextSingle(),
            System.Random.Shared.NextSingle()
        );
    }

    /// <summary>
    /// Converts this color to an 8-bit hexadecimal representation.
    /// </summary>
    /// <param name="includeAlpha">
    /// Whether the alpha component should be included.
    /// </param>
    /// <returns>
    /// A color in <c>#RRGGBB</c> or <c>#RRGGBBAA</c> format.
    /// </returns>
    public string ToHex(bool includeAlpha = true)
    {
        var color = Clamped();

        var red = ToByte(color.R);
        var green = ToByte(color.G);
        var blue = ToByte(color.B);
        var alpha = ToByte(color.A);

        return includeAlpha
            ? $"#{red:X2}{green:X2}{blue:X2}{alpha:X2}"
            : $"#{red:X2}{green:X2}{blue:X2}";
    }

    /// <summary>
    /// Returns the hexadecimal representation of this color.
    /// </summary>
    /// <returns>The color in <c>#RRGGBBAA</c> format.</returns>
    public override string ToString()
    {
        return ToHex();
    }

    /// <summary>
    /// Attempts to parse a hexadecimal color.
    /// </summary>
    /// <param name="value">The hexadecimal value to parse.</param>
    /// <param name="color">
    /// When this method returns, contains the parsed color if parsing succeeded;
    /// otherwise, contains the default color.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when parsing succeeds; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string? value, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hexadecimal = value.AsSpan().Trim();

        if (hexadecimal.StartsWith("#"))
        {
            hexadecimal = hexadecimal[1..];
        }
        else if (hexadecimal.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hexadecimal = hexadecimal[2..];
        }

        return hexadecimal.Length switch
        {
            3 => TryParseShort(hexadecimal, hasAlpha: false, out color),
            4 => TryParseShort(hexadecimal, hasAlpha: true, out color),
            6 => TryParseLong(hexadecimal, hasAlpha: false, out color),
            8 => TryParseLong(hexadecimal, hasAlpha: true, out color),
            _ => false,
        };
    }

    /// <summary>
    /// Returns this color with the specified alpha component.
    /// </summary>
    /// <param name="alpha">The new alpha component.</param>
    /// <returns>A copy of this color with the specified alpha.</returns>
    public Color WithAlpha(float alpha)
    {
        return new Color(R, G, B, alpha);
    }

    /// <summary>
    /// Adds the corresponding components of two colors.
    /// </summary>
    public static Color operator +(Color left, Color right)
    {
        return new Color(left.R + right.R, left.G + right.G, left.B + right.B, left.A + right.A);
    }

    /// <summary>
    /// Divides the corresponding components of two colors.
    /// </summary>
    public static Color operator /(Color left, Color right)
    {
        return new Color(left.R / right.R, left.G / right.G, left.B / right.B, left.A / right.A);
    }

    /// <summary>
    /// Divides every component of a color by a scalar.
    /// </summary>
    public static Color operator /(Color color, float scalar)
    {
        return new Color(color.R / scalar, color.G / scalar, color.B / scalar, color.A / scalar);
    }

    /// <summary>
    /// Multiplies the corresponding components of two colors.
    /// </summary>
    public static Color operator *(Color left, Color right)
    {
        return new Color(left.R * right.R, left.G * right.G, left.B * right.B, left.A * right.A);
    }

    /// <summary>
    /// Multiplies every component of a color by a scalar.
    /// </summary>
    public static Color operator *(Color color, float scalar)
    {
        return new Color(color.R * scalar, color.G * scalar, color.B * scalar, color.A * scalar);
    }

    /// <summary>
    /// Multiplies every component of a color by a scalar.
    /// </summary>
    public static Color operator *(float scalar, Color color)
    {
        return color * scalar;
    }

    /// <summary>
    /// Subtracts the corresponding components of two colors.
    /// </summary>
    public static Color operator -(Color left, Color right)
    {
        return new Color(left.R - right.R, left.G - right.G, left.B - right.B, left.A - right.A);
    }

    /// <summary>
    /// Negates every component of a color.
    /// </summary>
    public static Color operator -(Color color)
    {
        return new Color(-color.R, -color.G, -color.B, -color.A);
    }
}
