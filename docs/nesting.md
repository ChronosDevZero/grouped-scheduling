# Nesting

While a scheduled method is running, the runtime pushes its `ActionsHandler` onto an `AsyncLocal` nest stack.

| Call pattern | Result |
|---|---|
| Same instance / same handler re-enters another `[ScheduleGroup]` method | **Throws** |
| Object A awaits object B (different handlers) | **Allowed** |
| A → B → A | **Throws** (A already on the stack) |

Static and instance handlers on the same type are different handlers, so static → instance (or the reverse) is allowed if they are not the same nest frame.

## IDE vs runtime

- **TSCH001** catches many same-instance nests in source (direct / some obvious calls).
- **Runtime is authoritative**—reflection and some dynamic paths only fail when the call actually nests (see sample `NestRejectSmoke`).

## How to refactor

Extract non-scheduled helpers, or call through another instance / type so the nest stack sees two handlers.
