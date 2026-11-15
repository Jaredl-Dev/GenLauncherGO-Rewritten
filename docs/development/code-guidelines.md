# Code guidelines

## Shared rules

- Each shared rule has one authority, such as `LauncherFileSystemLayout` for executable and folder names,
  `LauncherPaths` for owned paths, `LauncherPreferencesDocumentMapper` for settings persistence, and
  `RemoteLauncherCatalogMapper` for the backend YAML. Reuse or move an authority; never copy its rule.

## Types and abstractions

- Keep types `internal sealed` unless binding, serialization, a framework contract such as an Avalonia control, or an
  actual subclass requires otherwise; the application has no external API consumers.
- Remove superseded code, unused APIs, old names, adapters, and compatibility shims in the same change; leave no
  parallel path without a current caller.
- Do not add speculative extension points or edge cases; require current behavior, an external contract, a reproduced
  defect, or a safety invariant.
- Fixed arguments or localization keys do not justify a type.

| New artifact | Allowed only when |
| --- | --- |
| Interface | It is an external or side-effect boundary, or has multiple production implementations. Testing convenience alone is insufficient. |
| Request | It validates a stable operation boundary or is genuinely shared; never just bundle arguments for one internal call. |
| Result | Callers branch on named outcomes or need structured failure data; never just mirror returned properties. |
| Factory | It selects implementations or owns meaningful construction or lifetime policy; never merely call `new`. |
| Coordinator | It owns sequencing, state, rollback, or lifecycle; never merely forward calls or group dependencies. |
| Mapper or DTO | It crosses an external or persistence boundary. Map once; do not add an intermediate mirror model. |
| Wrapper | It adds an invariant, ownership, or policy. Otherwise call the existing type directly. |

## Side effects and safety

- Never remove, bypass, or weaken a safety or concurrency mechanism without a maintainer's approval; raise it before
  changing it. These include containment, ownership, and reparse-point checks, atomic writes, staging cleanup,
  deployment journaling, rollback and recovery, the hard-link-to-copy fallback, catalog mutation serialization,
  package leases and admission, process-family tracking, and operation snapshots.
- Fail closed when path safety cannot be proven before traversing or mutating owned content.
- Read `LauncherRuntimePathContext.ActivePaths` once, before scheduling work, and keep that snapshot; switching games
  can replace the active paths while the work runs.
- Preserve existing persistence, journal, and recovery file formats, so data and interrupted launches from earlier
  versions still load.
- Never log credentials or tokens, and never write logs that bypass `SensitiveDataRedactingTextFormatter`, which strips
  user paths and secrets.
- Pass `CancellationToken` through asynchronous boundaries.

## Avalonia and localization

- Keep domain decisions, persistence, and launch or update workflows out of code-behind; it handles only focus, layout,
  animation, window chrome, visual-tree mechanics, and event forwarding.
- Respect Avalonia's UI-thread affinity, including for application resources.
- Put user-visible text in `GenLauncherGO/Resources/Strings*.resx`, translated into every locale rather than copied
  from English.

## Comments

- Document external contracts and non-obvious side effects, invariants, compatibility constraints, or platform
  behavior. Do not document obvious implementation details.
