namespace Mirage.Common.Primitives;

/// <summary>
/// Represents a phase of one-time preparation.
/// </summary>
public enum PreparationState
{
    /// <summary>Composition has not started.</summary>
    Unprepared,

    /// <summary>Composition is executing.</summary>
    Composing,

    /// <summary>Composition completed; configuration has not started.</summary>
    Composed,

    /// <summary>Configuration is executing.</summary>
    Configuring,

    /// <summary>Composition and configuration completed successfully.</summary>
    Prepared,

    /// <summary>A phase failed and preparation cannot be retried.</summary>
    Failed,
}
