using System.Numerics;
using OpenGE.Common.Lifecycle;
using OpenGE.Graphics.Resources;
using OpenGE.Windowing;
using Xunit;

namespace Tests.Windowing;

public class CursorTests
{
    [Fact]
    public void Constructor_UsesDefaultsAndConfiguredOptions()
    {
        var defaultCursor = new TestCursor("default");
        var image = new Image(1, 1, new byte[] { 1, 2, 3, 4 });
        var configuredCursor = new TestCursor(
            "custom",
            new CursorOptions
            {
                Hotspot = new Vector2(2, 3),
                Icon = image,
                SystemIcon = SystemCursor.Pointer,
                Visible = false,
            }
        );

        Assert.Equal(Vector2.Zero, defaultCursor.Hotspot.Get());
        Assert.Null(defaultCursor.Icon.Get());
        Assert.Equal(SystemCursor.Default, defaultCursor.SystemIcon.Get());
        Assert.True(defaultCursor.Visible.Get());
        Assert.Equal("default", defaultCursor.Identifier);

        Assert.Equal(new Vector2(2, 3), configuredCursor.Hotspot.Get());
        Assert.Same(image, configuredCursor.Icon.Get());
        Assert.Equal(SystemCursor.Pointer, configuredCursor.SystemIcon.Get());
        Assert.False(configuredCursor.Visible.Get());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhitespaceIdentifier_ThrowsArgumentException(string identifier)
    {
        Assert.Throws<ArgumentException>(() => new TestCursor(identifier));
    }

    [Fact]
    public void StoreChanges_InvokePlatformHooks()
    {
        var cursor = new TestCursor("cursor");
        var image = new Image(1, 1, new byte[] { 1, 2, 3, 4 });
        var hotspot = new Vector2(4, 5);

        cursor.Hotspot.Set(hotspot);
        cursor.Icon.Set(image);
        cursor.SystemIcon.Set(SystemCursor.Crosshair);
        cursor.Visible.Set(false);

        Assert.Equal(new[] { hotspot }, cursor.HotspotChanges);
        Assert.Equal(new[] { image }, cursor.IconChanges);
        Assert.Equal(new[] { SystemCursor.Crosshair }, cursor.SystemIconChanges);
        Assert.Equal(new[] { false }, cursor.VisibilityChanges);
    }

    [Fact]
    public void Apply_InvokesPlatformHookAndRejectsDestroyedCursor()
    {
        var cursor = new TestCursor("cursor");

        cursor.Apply();
        cursor.Destroy();

        Assert.Equal(1, cursor.ApplyCount);
        Assert.Throws<DestroyedObjectException>(cursor.Apply);
    }

    [Fact]
    public void Destroy_DestroysStoresButDoesNotDestroyBorrowedImage()
    {
        var image = new Image(1, 1, new byte[] { 1, 2, 3, 4 });
        var cursor = new TestCursor("cursor", new CursorOptions { Icon = image });

        cursor.Destroy();

        Assert.True(cursor.Destroyed);
        Assert.True(cursor.Hotspot.Destroyed);
        Assert.True(cursor.Icon.Destroyed);
        Assert.True(cursor.SystemIcon.Destroyed);
        Assert.True(cursor.Visible.Destroyed);
        Assert.False(image.Destroyed);
    }

    private sealed class TestCursor(string identifier, CursorOptions? options = null)
        : Cursor(identifier, options)
    {
        public int ApplyCount { get; private set; }
        public List<Vector2> HotspotChanges { get; } = [];
        public List<Image?> IconChanges { get; } = [];
        public List<SystemCursor> SystemIconChanges { get; } = [];
        public List<bool> VisibilityChanges { get; } = [];

        protected override void OnApply()
        {
            ApplyCount++;
        }

        protected override void OnHotspotChanged(Vector2 hotspot) => HotspotChanges.Add(hotspot);

        protected override void OnIconChanged(Image? icon) => IconChanges.Add(icon);

        protected override void OnSystemIconChanged(SystemCursor systemIcon) =>
            SystemIconChanges.Add(systemIcon);

        protected override void OnVisibilityChanged(bool visible) => VisibilityChanges.Add(visible);
    }
}
