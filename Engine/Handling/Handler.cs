using Mirage.Common;
using Mirage.Common.Collections;
using Mirage.Common.Primitives;
using Mirage.Handling.Devices;
using Mirage.Logging;
using Mirage.Scheduling;
using Mirage.Scheduling.Interfaces;
using Mirage.Windowing;

namespace Mirage.Handling;

/// <summary>
/// Registers input devices and updates them for one window.
/// </summary>
/// <remarks>
/// Constructor-provided and composed devices belong to the handler.
/// The window is borrowed and is never destroyed by the handler.
/// Platform-specific implementations provide the input context used by the devices.
/// </remarks>
public abstract class InputHandler : Module, IUpdatable
{
    private readonly IdentifiableSet<string, InputDevice> _devices = [];
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the read-only identifiable set of devices owned by this handler.
    /// </summary>
    public readonly IReadOnlyIdentifiableSet<string, InputDevice> Devices;

    /// <summary>
    /// Gets the window associated with this handler.
    /// </summary>
    public readonly Window Window;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputHandler"/> class.
    /// </summary>
    /// <param name="index">The index used to distinguish the module identifier.</param>
    /// <param name="window">The window associated with this handler.</param>
    /// <param name="devices">The devices to register initially, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initial devices contain duplicate identifiers.
    /// </exception>
    protected InputHandler(int index, Window window, IEnumerable<InputDevice>? devices = null)
        : base($"InputHandler-{index}", ["Logger"])
    {
        Window = window;

        foreach (var device in devices ?? [])
            _devices.Add(device);

        Devices = _devices;
    }

    private SourcedLogger Logger => Require<Logger>("Logger").From(Identifier);

    /// <inheritdoc />
    public virtual void Update(UpdateContext updateContext)
    {
        ThrowIfDestroyed();

        if (State.Get() != ModuleState.Running)
        {
            throw new InvalidOperationException(
                "The module must be running before processing updates."
            );
        }

        var context = CreateContext();

        foreach (var device in _devices)
        {
            try
            {
                device.Process(context);
            }
            catch (Exception exception)
            {
                Logger.Log(
                    $"Input device '{device.Identifier}' failed: {exception.Message}",
                    LogMessageKind.Warn
                );

                throw;
            }
        }
    }

    private void ComposeContents()
    {
        Logger.Log("Composing module contents.", LogMessageKind.Debug);

        foreach (var device in Compose())
        {
            try
            {
                _devices.Add(device);
            }
            catch
            {
                if (!_devices.Contains(device) && !device.Destroyed)
                    device.Destroy();

                throw;
            }
        }

        Logger.Log("Composition completed.", LogMessageKind.Debug);
    }

    private void ConfigureContents()
    {
        Logger.Log("Configuring module.", LogMessageKind.Debug);

        Configure();

        Logger.Log("Configuration completed.", LogMessageKind.Debug);
    }

    /// <summary>
    /// Composes the devices managed by this object.
    /// </summary>
    protected virtual IEnumerable<InputDevice> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition.
    /// </summary>
    protected virtual void Configure() { }

    /// <summary>
    /// Creates the platform-specific input context used for one update.
    /// </summary>
    /// <returns>The input context to provide to registered devices.</returns>
    protected abstract InputContext CreateContext();

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var device in _devices.ToArray())
        {
            device.Destroy();

            if (InjectedDependencies is not null)
            {
                Logger.Log($"Destroyed input device '{device.Identifier}'.", LogMessageKind.Debug);
            }
        }

        _devices.Destroy();

        if (InjectedDependencies is not null)
            Logger.Log("Module resources destroyed.");
    }

    /// <inheritdoc />
    protected override void OnStart()
    {
        Logger.Log("Starting module.");

        _preparator.Ensure(ComposeContents, ConfigureContents);

        foreach (var device in _devices)
        {
            Logger.Log(
                $"Input device '{device.Identifier}' ready for window '{Window.Identifier}'.",
                LogMessageKind.Debug
            );
        }

        Logger.Log("Module started.");
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        Logger.Log("Module stopped.");
        base.OnStop();
    }
}
