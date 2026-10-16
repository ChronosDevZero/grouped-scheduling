# Files: `InstanceSchedulingState.cs` + `StaticSchedulingState.cs`

**Paths:**

- `GroupedScheduling/InstanceSchedulingState.cs`
- `GroupedScheduling/StaticSchedulingState.cs`

Both are `public` + `[EditorBrowsable(Never)]`.

## Why two types?

Same plumbing (`SchedulingOwnerState`), different **lifetime and naming**:

| Type | Owner key | Token alias | Typical weave field |
|---|---|---|---|
| `InstanceSchedulingState` | object instance | `ObjectToken` | `__groupedSchedulingState` |
| `StaticSchedulingState` | declaring `Type` | `TypeToken` | `__groupedSchedulingStaticState` |

Keeping separate CLR types lets the weaver and hooks overload cleanly (`Schedule` vs `ScheduleStatic`) and makes debugging obvious in the watch window.

## Full sources

```csharp
// InstanceSchedulingState.cs
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InstanceSchedulingState : SchedulingOwnerState
{
    public CancellationToken ObjectToken => OwnerToken;

    public InstanceSchedulingState(Dictionary<int, EventActionGroup> groupMap)
        : base(groupMap)
    {
    }
}
```

```csharp
// StaticSchedulingState.cs
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StaticSchedulingState : SchedulingOwnerState
{
    public CancellationToken TypeToken => OwnerToken;

    public StaticSchedulingState(Dictionary<int, EventActionGroup> groupMap)
        : base(groupMap)
    {
    }
}
```

## Line walkthrough

### Inheritance

Both `sealed` subclasses call `base(groupMap)`, which constructs the `ActionsHandler`. No extra fields — the distinction is **semantic**.

### Token aliases

```csharp
public CancellationToken ObjectToken => OwnerToken;
public CancellationToken TypeToken => OwnerToken;
```

Same underlying CTS. Names document intent at call sites:

```csharp
state.Handler.ScheduleTask(work, groupId, state.ObjectToken);  // instance hooks
state.Handler.ScheduleTask(work, groupId, state.TypeToken);    // static hooks
```

When a method has its own `CancellationToken`, hooks call `CreateCallToken(methodToken)` instead of passing the raw owner token alone.

## Lifetime rules (product truth)

```text
  INSTANCE
  --------
  new Foo()  -> registry GetOrCreateInstanceState(foo, factory)
  foo.Dispose() / DI dispose -> InstanceSchedulingState.Dispose
  GC alone does NOT shut down scheduling  (do not rely on finalizers)

  STATIC
  ------
  First static scheduled call on Type T
       -> registry GetOrCreateStaticState(typeof(T), factory)
  Lives until HandlerRegistry.ShutdownAll* (host stop)
```

**Closed generics:** `Foo<int>` and `Foo<string>` are different `Type` keys → different static states / handlers.

**Instance vs static on the same class:** independent handlers. Nest allows static → instance (and reverse) because the nest stack keys on handler identity.

## Who holds the references?

```text
  Instance: ConditionalWeakTable<object, InstanceSchedulingState>
            + instance field injected by weaver (fast path)

  Static:   ConcurrentDictionary<Type, StaticSchedulingState>
            + static field injected by weaver
```

The registry keeps process-wide discoverability for host shutdown. The injected fields keep hot calls cheap.

## What breaks if…

| Change | Failure |
|---|---|
| Collapse to one state type with a flag | Easy to pass the wrong token; weaver binding gets messier |
| Key static state by open generic definition only | All closed generics would share one lane unintentionally |
| Rely on GC for instance shutdown | Tokens never cancel; handlers linger |

## See also

- `HandlerRegistry` (next)
- Weave field name constants on `SchedulingWeaveHooks`
- Sample: `StaticOnlySmoke`, lifecycle demos
