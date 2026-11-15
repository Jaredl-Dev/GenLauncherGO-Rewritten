# Testing

Tests mirror the application's `Features` and `Shared` folders under `GenLauncherGO.Tests`, with shared helpers in
`GenLauncherGO.Tests/Testing`. [Required quality gates](../../CONTRIBUTING.md#required-quality-gates) shows how to run
them.

## When to add a test

Add a test only when a maintainer asks for one, or when the change touches a safety invariant (owned-path
containment, reparse points, ownership, or elevation), the backend YAML contract, or behavior whose regression would
lose user data, damage a game folder, or lock users out. A minor or low-impact fix, a rare UX fallback, or a regression
whose worst case is a declinable prompt or a different error message does not earn a test.

A new test must fail for the intended reason, with the behavior it guards absent or broken rather than through a build
error, before the change makes it pass.

## Writing tests

- Never test implementation mirroring, framework behavior, private helpers, or DI registrations beyond the one
  composition test. Use the fewest representative scenarios; overlap only where destructive file-system or security
  boundaries need defense in depth.
- Keep headless Avalonia tests semantic: assert that compiled AXAML loads and that meaningful user states expose the
  expected content, actions, accessibility, and theme resources. Never assert coordinates, margins, grid positions,
  control dimensions, template parts, or visual-tree shape.
- Never assert real-time animation midpoints or write no-throw framework smoke tests; test application-owned state
  transitions and outcomes.
- Use isolated temporary directories for file-system tests. Never require a real game installation, live network
  service, or production credential.
- Split a test that has repeated act phases or branching assertions.
- Add a helper to `Testing/` only once a second test needs it.

## Helper names

| Prefix | Means |
| --- | --- |
| `Fake` | A hand-written working implementation, simplified but with real behavior and state. |
| `Recording` | Captures the calls a test asserts on, exposed as `List<>` properties. |
| `Stub` | Answers with fixed values and records nothing. |
| `Controllable` | The test decides when the operation completes, usually through a `TaskCompletionSource`. |
| `Test` | Builds inputs or supplies shared test values — paths, content, view models, timeouts. Not a test double. |

A helper whose own name says more than the prefix keeps that name instead: `CompletedGameProcessLaunchOperation`,
`QueueHttpMessageHandler`, `StaTestRunner`. Scopes that restore state on dispose end in `Scope`, and static helpers
that create junctions or symbolic links and report whether the machine allows them end in `Support`.

## Symbolic-link safety tests

Symbolic-link tests are required in CI and fail the workflow if the runner cannot execute them. Local accounts that
cannot create symbolic links report those tests as explicit skips. To enforce the same fail-closed behavior locally,
enable Windows Developer Mode or use an elevated terminal, then run:

```powershell
dotnet test GenLauncherGO.Tests/GenLauncherGO.Tests.csproj -c Release -e GENLAUNCHERGO_REQUIRE_SYMBOLIC_LINK_TESTS=true
```

## Coverage

Optionally generate a local coverage report:

```powershell
dotnet msbuild ./eng/coverage.proj -target:Coverage
```

The HTML report is written to `artifacts/coverage/index.html`. Coverage is informational: CI does not enforce a
percentage because executing a line does not establish that its behavior is meaningfully protected.
