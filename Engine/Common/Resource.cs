using OpenGE.Common.Lifecycle;

namespace OpenGE.Common;

/// <summary>
/// Represents reusable data that can be shared between objects.
/// </summary>
/// <remarks>
/// A resource is destroyed by its creator or by the owner to which it was transferred,
/// such as an importer. Objects using a shared resource borrow it and do not destroy it.
/// </remarks>
public abstract class Resource : Destroyable { }
