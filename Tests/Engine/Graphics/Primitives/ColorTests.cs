using OpenGE.Graphics.Primitives;
using Xunit;

namespace Tests.Graphics.Primitives;

public class ColorTests
{
    [Fact]
    public void Default_IsFullyTransparent()
    {
        Color color = default;

        Assert.Equal(0f, color.R);
        Assert.Equal(0f, color.G);
        Assert.Equal(0f, color.B);
        Assert.Equal(0f, color.A);
    }

    [Fact]
    public void Constructor_WithoutArguments_IsFullyTransparent()
    {
        var color = new Color();

        Assert.Equal(Color.Transparent, color);
    }

    [Fact]
    public void Constructor_ExplicitComponents_DefaultsAlphaToOpaque()
    {
        var color = new Color(0f, 0f, 0f);

        Assert.Equal(Color.Black, color);
        Assert.Equal(1f, Color.Red.A);
    }

    [Fact]
    public void TryParse_InvalidValue_ReturnsTransparentColor()
    {
        var parsed = Color.TryParse("not-a-color", out var color);

        Assert.False(parsed);
        Assert.Equal(Color.Transparent, color);
    }

    [Theory]
    [InlineData("#F00", 1f, 0f, 0f, 1f)]
    [InlineData("#00FF00", 0f, 1f, 0f, 1f)]
    [InlineData("#0000FFFF", 0f, 0f, 1f, 1f)]
    public void FromHex_ParsesSupportedFormats(
        string hexadecimal,
        float red,
        float green,
        float blue,
        float alpha
    )
    {
        var color = Color.FromHex(hexadecimal);

        Assert.Equal(red, color.R, precision: 5);
        Assert.Equal(green, color.G, precision: 5);
        Assert.Equal(blue, color.B, precision: 5);
        Assert.Equal(alpha, color.A, precision: 5);
    }
}
