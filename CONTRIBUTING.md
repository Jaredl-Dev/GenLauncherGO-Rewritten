# Contributing to GenLauncherGO

Contributions are welcome. Fork the repository, create a branch, make and test your changes, then open a pull request
as described in [Pull requests](#pull-requests).

[Architecture](docs/development/architecture.md) describes how the code is organized and the backend contract it must
preserve. Maintainers publishing a version follow [Releasing](docs/development/releasing.md).

## Development setup

The repository selects the .NET 10 SDK through `global.json`, starting at version `10.0.300` and allowing later
feature bands.

Run the Avalonia project from the repository root:

```powershell
dotnet run --project ./GenLauncherGO/GenLauncherGO.csproj
```

For a full launcher UI session, run the executable outside all supported game installations. Startup validation
blocks the launcher when it is placed inside one.

## Required quality gates

Build the solution after every code change, then verify formatting for the files you changed and run the tests for
the affected feature:

```powershell
dotnet build GenLauncherGO.sln
dotnet format GenLauncherGO.sln --verify-no-changes --include <changed files>
dotnet test GenLauncherGO.Tests/GenLauncherGO.Tests.csproj --filter-namespace GenLauncherGO.Tests.Features.Mods
```

To run a single test class, use `--filter-class` with its fully qualified name. Run the full checks for shared
build/analyzer changes, broad refactors, or changes whose impact cannot be covered narrowly:

```powershell
dotnet build GenLauncherGO.sln
dotnet format GenLauncherGO.sln --verify-no-changes
dotnet test GenLauncherGO.Tests/GenLauncherGO.Tests.csproj -c Release
```

The Windows CI workflow runs the full checks in Release. Changes to the updater or release process also need the
[update smoke test](docs/development/update-smoke-test.md).

## Testing policy

Admit a test only if it would catch a realistic regression in important observable behavior, an external contract, or a
safety invariant, or prevent a costly failure. The existing recovery, path-safety, compatibility, persistence, race,
and workflow tests set the bar.

Never admit tests for coverage, code existence, implementation mirroring, routine properties or guards, framework
behavior, private helpers, DI details, or exhaustive low-value permutations. Prefer the fewest representative scenarios;
overlap only where destructive file-system or security boundaries require defense in depth.

Before implementation, identify the qualifying tests and their value, or state why none qualify. Write and narrowly run
each test first; red must be the intended behavioral failure, not a compilation, setup, unrelated, environmental, or
flaky failure. Only a characterization test required for a behavior-preserving refactor may bypass red, and that
exception must be explained before editing. After red, rerun the target to green and the smallest additional scope
needed to cover the affected behavior. Record the red and green commands and outcomes, or the no-test rationale, in
the pull request.

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

## Submitting changes

Keep changes focused, and preserve existing behavior unless the change deliberately updates it. Do not rename or
reshape the backend's remote YAML contract; see
[Backend compatibility](docs/development/architecture.md#backend-compatibility). The [testing policy](#testing-policy)
decides which tests to include.

### Commit format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```text
type(scope): Short imperative summary
```

- **Types**: `feat`, `fix`, `perf`, `refactor`, `style`, `test`, `docs`, `build`, `ci`, `chore`.
- **Scope**: Optional, naming the affected feature or shared area in lowercase, usually a folder under `Features` or
  `Shared` (e.g., `mods`, `updating`, `io`, `themes`).
- Use the imperative mood starting with a capital letter (e.g., `fix(mods): Resolve manifest destination collision`). Keep commits atomic and focused.

### Pull requests

Open pull requests against `main` with a [Conventional Commits title](#commit-format). Put related issue or pull
request links first when applicable. Explain the existing behavior or problem, what changed, and why, with enough
detail for a reviewer to understand the intent before opening the diff.

Describe relevant verification and its results (or why it was not run), and note compatibility effects or known
limitations when they matter. Follow the [testing policy](#testing-policy) for test evidence. Use headings, tables, or
screenshots when they clarify a substantial change; there are no required body sections.
