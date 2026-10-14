using OpenGE.Common.Events;
using OpenGE.Common.Interfaces;
using OpenGE.Common.Lifecycle;
using OpenGE.Common.Primitives;
using OpenGE.Graphics;
using OpenGE.Graphics.Commands;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Resources.Targets;

namespace OpenGE.Rendering.Spaces;

/// <summary>
/// Represents the rendering priority of a space.
/// </summary>
public enum RenderSpacePriority
{
    /// <summary>Collected before normal-priority spaces.</summary>
    Low,

    /// <summary>The default rendering priority.</summary>
    Normal,

    /// <summary>Collected after normal-priority spaces.</summary>
    High,

    /// <summary>Collected after all other priorities.</summary>
    Critical,
}

/// <summary>
/// Groups renderable objects and collects their rendering commands into a single space.
/// </summary>
/// <remarks>
/// A rendering space organizes renderable objects and produces their commands in
/// entry order. The space does not execute rendering commands or depend on a
/// specific rendering backend.
///
/// Renderable entries are borrowed by the space and are not destroyed with it.
/// </remarks>
public class RenderSpace : Destroyable, IIdentifiable<string>
{
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the camera used to render this space.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> camera represents screen-space rendering.
    /// </remarks>
    public readonly Store<ICamera> Camera;

    /// <summary>
    /// Gets the renderable objects contained in this space.
    /// </summary>
    /// <remarks>
    /// The space references its entries without owning or destroying them.
    /// </remarks>
    public readonly List<IRenderable> Entries = [];

    /// <summary>
    /// Gets the priority that determines when this space is collected.
    /// </summary>
    public readonly RenderSpacePriority Priority;

    /// <summary>
    /// Gets the offscreen rendering target, or null to render to the window.
    /// </summary>
    /// <remarks>
    /// The target is borrowed and is not destroyed with the space.
    /// </remarks>
    public readonly Store<RenderTarget?> Target;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderSpace"/> class.
    /// </summary>
    /// <param name="identifier">
    /// The space's unique identifier.
    /// </param>
    /// <param name="camera">
    /// The initial camera used to render this space.
    /// </param>
    /// <param name="target"></param>
    /// <param name="priority">
    /// The rendering priority. The default is
    /// <see cref="RenderSpacePriority.Normal"/>.
    /// </param>
    /// <param name="entries">
    /// The initial renderable references, or <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="identifier"/> is empty or whitespace.
    /// </exception>
    public RenderSpace(
        string identifier,
        ICamera camera,
        RenderTarget? target = null,
        RenderSpacePriority priority = RenderSpacePriority.Normal,
        IEnumerable<IRenderable>? entries = null
    )
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Space identifier cannot be empty.", nameof(identifier));

        Identifier = identifier;
        Camera = new Store<ICamera>(camera);
        Target = new Store<RenderTarget?>(target);
        Priority = priority;

        foreach (var entry in entries ?? [])
            Entries.Add(entry);
    }

    /// <inheritdoc />
    public string Identifier { get; }

    private void ComposeContents()
    {
        var composedObjects = Compose().ToArray();

        foreach (var entry in composedObjects)
            Entries.Add(entry);
    }

    private void ConfigureContents()
    {
        Configure();
    }

    /// <summary>
    /// Composes the renderable entries contained in this space.
    /// </summary>
    /// <returns>
    /// The renderable entries to register, in enumeration order.
    /// </returns>
    /// <remarks>
    /// Composition occurs once before the first collection.
    /// Constructor-provided entries are registered before composed entries.
    /// All composed entries are registered before configuration occurs.
    ///
    /// The space borrows entries, including those returned here, because
    /// renderable objects have independent owners and may participate in
    /// multiple rendering spaces. Composition does not transfer ownership
    /// of those objects to the space.
    ///
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<IRenderable> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition and before
    /// the first collection.
    /// </summary>
    /// <remarks>
    /// All constructor-provided and composed entries are available when this
    /// hook is invoked.
    ///
    /// This hook is invoked at most once. If configuration throws, later
    /// lifecycle calls reject further initialization rather than repeating
    /// configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <summary>
    /// Collects additional rendering commands after the space's entries.
    /// </summary>
    /// <param name="context">
    /// The rendering context used to produce rendering data.
    /// </param>
    /// <returns>
    /// Additional rendering commands to append to the space, in execution order.
    /// </returns>
    protected virtual IEnumerable<IRenderCommand> OnCollect(IRenderContext context)
    {
        yield break;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Clears the entry list without destroying its renderable objects.
    /// </remarks>
    protected override void OnDestroy()
    {
        Entries.Clear();
        Camera.Destroy();
        Target.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Collects the rendering commands produced by this space's entries.
    /// </summary>
    /// <param name="context">
    /// The rendering context used by entries to produce their rendering data.
    /// </param>
    /// <returns>
    /// The rendering commands produced by the space, preserving entry and
    /// command order.
    /// </returns>
    /// <remarks>
    /// Entries are evaluated in their registration order. Commands produced
    /// by each entry are appended in the order in which they appear in its
    /// <see cref="RenderData"/>.
    ///
    /// The returned commands are not executed by the space. Execution is the
    /// responsibility of the rendering system and its active backend.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when this space has been destroyed.
    /// </exception>
    public IReadOnlyList<IRenderCommand> Collect(IRenderContext context)
    {
        ThrowIfDestroyed();

        _preparator.Ensure(ComposeContents, ConfigureContents);

        var commands = new List<IRenderCommand>();

        foreach (var data in Entries.Select(entry => entry.Render(context)))
        {
            commands.AddRange(data.Commands);
        }

        commands.AddRange(OnCollect(context));

        return commands;
    }
}
