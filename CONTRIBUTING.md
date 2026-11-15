# Contributing to GenLauncherGO

Fork the repository, create a branch, make and test your changes, then open a pull request as described in
[Pull requests](#pull-requests).

Open these development docs when your work calls for them:

- [Code guidelines](docs/development/code-guidelines.md): before changing application code.
- [Architecture](docs/development/architecture.md): before deciding where new code goes, changing service registration
  or project references, or touching the backend YAML contract.
- [Testing](docs/development/testing.md): before adding or changing tests, or running the symbolic-link or coverage
  checks.
- [Releasing](docs/development/releasing.md): for release or packaging work.

## Development setup

Install the .NET SDK that [`global.json`](global.json) selects, then run the Avalonia project from the repository root:

```powershell
dotnet run --project ./GenLauncherGO/GenLauncherGO.csproj
```

For a full launcher UI session, run the executable outside all supported game installations. Startup validation
blocks the launcher when it is placed inside one.

## Required quality gates

Before opening a pull request, run the checks the Windows CI workflow runs:

```powershell
dotnet build GenLauncherGO.sln -c Release
dotnet format GenLauncherGO.sln --verify-no-changes
dotnet test GenLauncherGO.sln -c Release --no-build
```

CI also enforces the [symbolic-link safety tests](docs/development/testing.md#symbolic-link-safety-tests), which can
skip on your machine.

Never make a change compile or a test pass by weakening a gate: no new `.editorconfig` severity downgrade or opt-out,
no suppression or `#pragma`, no `NoWarn`, no analyzer or warnings-as-errors property change, and no skipped or deleted
test. If a gate is wrong because it reports false positives, its fixer corrupts source, or its remedy costs more than
the defect it names, raise it instead of working around it.

Changes to the updater or release process also need the [update smoke test](docs/development/update-smoke-test.md).

## Submitting changes

Keep each change and commit focused, and preserve existing behavior unless the change deliberately updates it.

### Commit format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```text
type(scope): Short imperative summary
```

- **Types**: `feat`, `fix`, `perf`, `refactor`, `style`, `test`, `docs`, `build`, `ci`, `chore`.
- **Scope**: Optional, naming the affected feature or shared area in lowercase, usually a folder under `Features` or
  `Shared` (e.g., `mods`, `updating`, `io`, `themes`).
- Use the imperative mood starting with a capital letter (e.g., `fix(mods): Resolve manifest destination collision`).

### Pull requests

Open pull requests against `main` with a [Conventional Commits title](#commit-format). Put related issue or pull
request links first when applicable. Explain the existing behavior or problem, what changed, and why, with enough
detail for a reviewer to understand the intent before opening the diff.

Describe relevant verification and its results (or why it was not run), and note compatibility effects or known
limitations when they matter. Use headings, tables, or screenshots when they clarify a substantial change; there are
no required body sections.
