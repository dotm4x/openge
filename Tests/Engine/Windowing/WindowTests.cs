using System.Numerics;
using OpenGE.Common.Lifecycle;
using OpenGE.Scheduling;
using OpenGE.Windowing;
using Xunit;

namespace Tests.Windowing;

public class WindowTests
{
    [Fact]
    public void Constructor_UsesDefaultsAndConfiguredOptions()
    {
        var defaultWindow = new TestWindow();
        var cursor = new TestCursor("main");
        var configuredWindow = new TestWindow(
            new WindowOptions
            {
                Identifier = "game",
                Title = "Game",
                Size = new Vector2(1280, 720),
                Position = new Vector2(50, 70),
                Mode = WindowMode.Maximized,
                Resizable = false,
                Visible = false,
                Cursors = [cursor],
            }
        );

        Assert.Equal("window", defaultWindow.Identifier);
        Assert.Equal("OpenGE", defaultWindow.Title.Get().Split(" - ")[0]);
        Assert.Equal(new Vector2(800, 600), defaultWindow.Size.Get());
        Assert.Equal(Vector2.Zero, defaultWindow.Position.Get());
        Assert.False(defaultWindow.HasConfiguredPosition);
        Assert.True(defaultWindow.Resizable.Get());
        Assert.True(defaultWindow.Visible.Get());
        Assert.Equal(WindowMode.Normal, defaultWindow.Mode.Get());

        Assert.Equal("game", configuredWindow.Identifier);
        Assert.Equal("Game", configuredWindow.Title.Get());
        Assert.Equal(new Vector2(1280, 720), configuredWindow.Size.Get());
        Assert.Equal(new Vector2(50, 70), configuredWindow.Position.Get());
        Assert.True(configuredWindow.HasConfiguredPosition);
        Assert.False(configuredWindow.Resizable.Get());
        Assert.False(configuredWindow.Visible.Get());
        Assert.Equal(WindowMode.Maximized, configuredWindow.Mode.Get());
        Assert.Same(cursor, configuredWindow.Cursor.Get());
        Assert.Same(cursor, configuredWindow.Cursors["main"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhitespaceIdentifier_ThrowsArgumentException(string identifier)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new TestWindow(new WindowOptions { Identifier = identifier })
        );

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Constructor_DuplicateCursorIdentifiers_ThrowsInvalidOperationException()
    {
        var cursors = new[] { new TestCursor("same"), new TestCursor("same") };

        Assert.Throws<InvalidOperationException>(() =>
            new TestWindow(new WindowOptions { Cursors = cursors })
        );
    }

    [Fact]
    public void OpenAndClose_ChangesStateAndRaisesClosingBeforePlatformClose()
    {
        var window = new TestWindow();
        var events = new List<string>();
        window.OnClosing.Connect(_ => events.Add($"closing:{window.Opened.Get()}"));
        window.OnCloseAction = () => events.Add($"close:{window.Opened.Get()}");

        window.Open();
        window.Open();
        Assert.True(window.Opened.Get());

        window.Close();
        window.Close();

        Assert.Equal(new[] { "closing:True", "close:True" }, events);
        Assert.False(window.Opened.Get());
        Assert.False(window.Focused.Get());
        Assert.Equal(1, window.OpenCount);
        Assert.Equal(1, window.CloseCount);
    }

    [Fact]
    public void Update_WhenOpen_ProcessesPlatformEvents()
    {
        var window = new TestWindow();
        var context = new UpdateContext(0.25, 30);

        window.Update(context);
        Assert.Equal(0, window.ProcessCount);

        window.Open();
        window.Update(context);

        Assert.Equal(1, window.ProcessCount);
    }

    [Fact]
    public void RequestedStoreChanges_ApplyToPlatformHooks()
    {
        var window = new TestWindow();
        var size = new Vector2(1024, 768);
        var position = new Vector2(15, 25);

        window.Mode.Set(WindowMode.Fullscreen);
        window.Position.Set(position);
        window.Resizable.Set(false);
        window.Size.Set(size);
        window.Title.Set("Updated");
        window.Visible.Set(false);

        Assert.Equal(new[] { WindowMode.Fullscreen }, window.ModeChanges);
        Assert.Equal(new[] { position }, window.PositionChanges);
        Assert.Equal(new[] { false }, window.ResizableChanges);
        Assert.Equal(new[] { size }, window.SizeChanges);
        Assert.Equal(new[] { "Updated" }, window.TitleChanges);
        Assert.Equal(new[] { false }, window.VisibilityChanges);
    }

    [Fact]
    public void PlatformPublishedChanges_UpdateStoresWithoutReapplyingRequests()
    {
        var window = new TestWindow();
        var size = new Vector2(900, 700);
        var position = new Vector2(10, 20);

        window.PublishPlatformState(
            focused: true,
            mode: WindowMode.Maximized,
            position,
            resizable: false,
            size,
            title: "Platform title",
            visible: false
        );

        Assert.True(window.Focused.Get());
        Assert.Equal(WindowMode.Maximized, window.Mode.Get());
        Assert.Equal(position, window.Position.Get());
        Assert.False(window.Resizable.Get());
        Assert.Equal(size, window.Size.Get());
        Assert.Equal("Platform title", window.Title.Get());
        Assert.False(window.Visible.Get());
        Assert.Empty(window.ModeChanges);
        Assert.Empty(window.PositionChanges);
        Assert.Empty(window.ResizableChanges);
        Assert.Empty(window.SizeChanges);
        Assert.Empty(window.TitleChanges);
        Assert.Empty(window.VisibilityChanges);
    }

    [Fact]
    public void Destroy_WhenOpen_ClosesPlatformAndDestroysStoresButNotCursors()
    {
        var cursor = new TestCursor("main");
        var window = new TestWindow(new WindowOptions { Cursors = [cursor] });
        var closingCount = 0;
        window.OnClosing.Connect(_ => closingCount++);
        window.Open();

        window.Destroy();

        Assert.True(window.Destroyed);
        Assert.True(window.Opened.Destroyed);
        Assert.True(window.Mode.Destroyed);
        Assert.True(window.Cursor.Destroyed);
        Assert.True(window.Cursors.Destroyed);
        Assert.True(cursor.Destroyed is false);
        Assert.Equal(1, closingCount);
        Assert.Equal(1, window.CloseCount);
        Assert.Throws<DestroyedObjectException>(() => window.Open());
        Assert.Throws<DestroyedObjectException>(() => window.Update(default));
    }

    [Fact]
    public void Open_WhenPlatformOpeningThrows_LeavesWindowClosed()
    {
        var window = new TestWindow { OpenException = new InvalidOperationException() };

        Assert.Throws<InvalidOperationException>(window.Open);

        Assert.False(window.Opened.Get());
    }

    private sealed class TestWindow(WindowOptions? options = null) : Window(options)
    {
        public bool HasConfiguredPosition => HasInitialPosition;
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int ProcessCount { get; private set; }
        public Exception? OpenException { get; init; }
        public Action? OnCloseAction { get; set; }
        public List<WindowMode> ModeChanges { get; } = [];
        public List<Vector2> PositionChanges { get; } = [];
        public List<bool> ResizableChanges { get; } = [];
        public List<Vector2> SizeChanges { get; } = [];
        public List<string> TitleChanges { get; } = [];
        public List<bool> VisibilityChanges { get; } = [];

        public void PublishPlatformState(
            bool focused,
            WindowMode mode,
            Vector2 position,
            bool resizable,
            Vector2 size,
            string title,
            bool visible
        )
        {
            PublishFocused(focused);
            PublishMode(mode);
            PublishPosition(position);
            PublishResizable(resizable);
            PublishSize(size);
            PublishTitle(title);
            PublishVisible(visible);
        }

        protected override void OnClose()
        {
            CloseCount++;
            OnCloseAction?.Invoke();
        }

        protected override void OnModeChanged(WindowMode mode)
        {
            if (ShouldApply(Mode, mode))
                ModeChanges.Add(mode);
        }

        protected override void OnMove(Vector2 position)
        {
            if (ShouldApply(Position, position))
                PositionChanges.Add(position);
        }

        protected override void OnOpen()
        {
            OpenCount++;
            if (OpenException is not null)
                throw OpenException;
        }

        protected override void OnProcess()
        {
            ProcessCount++;
        }

        protected override void OnResizableChanged(bool resizable)
        {
            if (ShouldApply(Resizable, resizable))
                ResizableChanges.Add(resizable);
        }

        protected override void OnResize(Vector2 size)
        {
            if (ShouldApply(Size, size))
                SizeChanges.Add(size);
        }

        protected override void OnTitleChanged(string title)
        {
            if (ShouldApply(Title, title))
                TitleChanges.Add(title);
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (ShouldApply(Visible, visible))
                VisibilityChanges.Add(visible);
        }
    }

    private sealed class TestCursor(string identifier) : Cursor(identifier)
    {
        protected override void OnApply() { }

        protected override void OnHotspotChanged(Vector2 hotspot) { }

        protected override void OnIconChanged(OpenGE.Graphics.Resources.Image? icon) { }

        protected override void OnSystemIconChanged(SystemCursor systemIcon) { }

        protected override void OnVisibilityChanged(bool visible) { }
    }
}
