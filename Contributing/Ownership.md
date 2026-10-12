# Ownership and destruction

Mirage uses `Destroyable` for explicit lifetime management. Design ownership alongside an API, and make its destruction behavior match that contract. See [Documenting the API](Documenting.md) for how to express the contract in XML comments.

## Choose one owner

Every destroyable object should have one logical owner responsible for calling `Destroy()`. Other objects may borrow it, but holding a reference does not make them owners.

When deciding whether a relationship implies ownership, ask:

> If A is destroyed, does B still have an independent reason to exist?

If not, B probably belongs to A. If B is shared or has a separate lifetime, A probably borrows it. Also consider who creates, registers, and manages B. A shared object still needs an owner; “borrowed” must not mean “nobody destroys it.”

For example, a logger owns its registered outputs. A renderer borrows the window it renders into. Destroying the renderer releases its rendering resources, but does not destroy the window.

Public access to an owned object is borrowed access. Callers must not independently destroy an object while it still belongs to another owner.

## Give each collection one contract

For a manager's owning collection, registration transfers lifetime responsibility to the manager. Apply that rule consistently to constructor arguments, composition results, and objects registered later.

Collections such as `ReactiveSet<T>`, `ReactiveDictionary<TKey, TValue>`, and `IdentifiableSet<...>` do not themselves own their elements. The surrounding owner must destroy the elements explicitly:

```csharp
public class OutputGroup : Destroyable
{
    public readonly ReactiveSet<LogOutput> Outputs = [];

    protected override void OnDestroy()
    {
        foreach (var output in Outputs.ToArray())
            output.Destroy();

        Outputs.Destroy();
        base.OnDestroy();
    }
}
```

The snapshot allows an output to detach itself during destruction without invalidating iteration. Use this pattern where element cleanup can mutate the collection; it does not make arbitrary mutation during teardown safe.

For a borrowed collection, destroy the collection and release its references, but leave the elements alive. Likewise, destroying a `Store<Texture>` destroys the store and its subscriptions, not the texture stored inside it.

Prefer one contract per managed collection. Do not introduce ownership flags or separate owned/borrowed registration methods unless an actual use case requires mixed ownership.

## Treat composition as ownership by default

Objects returned by `Compose()` normally belong to the composer. Register them before `Configure()` so configuration can connect already available objects. Constructor-provided and composed objects entering the same collection must have the same ownership contract.

```csharp
protected override IEnumerable<LogOutput> Compose()
{
    yield return new ConsoleOutput();
}
```

Here the logger adopts the output and destroys it during teardown.

A processing view can be an explicit exception. An update channel may process modules already owned by an engine, for example. Such a `Compose()` method returns borrowed references and must document that exception. Do not create an otherwise ownerless destroyable inside a borrowed composition API.

Account for partial failure. Retain responsibility for objects already adopted if a later composition or configuration step throws. Release newly created objects that cannot be registered, without destroying objects that belong elsewhere. Iterators remain responsible for resources they allocate but never yield.

## Borrow dependencies and shared resources

`Require<TModule>()` and `InjectedDependencies` provide borrowed references. Consumers never destroy those modules.

```csharp
private SourcedLogger Logger => Require<Logger>("Logger").From(Identifier);
```

Using this logger does not transfer its lifetime to the current module. Its engine remains responsible for destruction.

Apply the same reasoning to shared textures, images, materials, windows, and scene objects. A wrapper may own its stores or native handles while borrowing the resource those objects refer to. State those distinctions explicitly.

## Define removal and transfer

Removing, replacing, or clearing entries in an owning collection normally returns responsibility for the removed objects to the caller without destroying them. This supports temporary detachment and reuse.

```csharp
firstLogger.Outputs.Remove(output);
secondLogger.Outputs.Add(output);
```

Between these operations, the caller owns `output`. If registration with the second owner fails, the caller must retain it or destroy it. Never register the same object with two owners at once.

Document restrictions imposed by the lifecycle, such as requiring an object to be inactive before removal or deferring removal until an update finishes. Removing a borrowed reference does not transfer ownership: its independent owner remains responsible for it.

## Preserve safe teardown order

An `OnDestroy()` override must release owned contents and honor the inherited lifecycle contract. Always call `base.OnDestroy()`, but choose its position based on that contract:

- Validate lifecycle preconditions before destructive work. A running module must not lose its resources before destruction is rejected.
- Keep stores and dependencies alive while unload hooks still need them.
- Release native resources while the handles or subsystems they depend on remain valid.
- Disconnect subscriptions to borrowed objects before the subscriber disappears.
- Release owned elements before their collection, and consumers before their dependencies.

Destruction is not the same as `Clear()`. Mirage's reactive collections disconnect their signals and discard references during destruction without issuing ordinary mutation notifications. Do not rely on removal callbacks to destroy owned elements.

Use `Destroyable` for explicit lifetimes. Do not replace it with finalizers, garbage-collection-based cleanup, or an unrelated disposal system.

## Review an ownership change

Check the constructor, composition path, runtime registration, removal, and teardown together. For each destroyable, identify its owner and trace the matching destruction path. Then check that borrowed dependencies are never destroyed, inherited cleanup still runs, failure paths do not abandon acquired resources, and XML documentation describes the resulting behavior.
