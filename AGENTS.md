# GenLauncherGO Agent Guidelines

Read [CONTRIBUTING.md](CONTRIBUTING.md) before changing any file, and open each development doc it lists only when its
trigger applies.

## Workflow

- For Avalonia work, call `get_avalonia_expert_rules` on the repository-configured `avalonia-docs` MCP server first.
  Beyond it, use only `search_avalonia_docs` and `lookup_avalonia_api`. Call no other tool from that server, and set up
  no Developer Tools, XPF, or other commercial Avalonia feature, unless the owner asks.
- Build the whole solution, then narrow the other quality gates to your change and everything that depends on it:
  `dotnet format GenLauncherGO.sln --verify-no-changes --include <changed files>` and
  `dotnet test GenLauncherGO.Tests/GenLauncherGO.Tests.csproj` with `--filter-namespace <namespace>` or
  `--filter-class <fully qualified class>`.
- Trust a diagnostic count only once the build reports no compiler errors. A project that fails to compile reports
  nothing of its own and hides every diagnostic in the projects downstream of it.
- Apply a bulk fix only at the sites the tool reported, never file-wide, then rebuild the whole solution. A change that
  compiles where you made it can still break callers in another project, and `dotnet format` rewrites code rather than
  only whitespace.
- Add or change release automation only when the owner asks; a published release reaches every installed launcher.
- Use `GenLauncherGO` for new names, and write `GeneralsOnline` as one word (never "Generals Online") in code,
  comments, documentation, and user-facing text.

## Completion

- In the handoff, list every new production interface, request, result, factory, coordinator, or wrapper and the
  [code guideline](docs/development/code-guidelines.md#types-and-abstractions) that justified it; say explicitly when
  none were added.
