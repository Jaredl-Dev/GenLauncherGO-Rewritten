# Releasing

`VersionPrefix` in `GenLauncherGO/GenLauncherGO.csproj` is the single source of truth for the release version. In
one pull request, update it and add a `[<VersionPrefix>] - YYYY-MM-DD` section to `CHANGELOG.md` that summarizes the
user-visible changes since the last release, then merge to `main`. `git log v<previous version>..main` lists the
commits to summarize. After CI passes, run **Actions** > **Publish release** on `main`.

The workflow builds the Windows x64 portable package and creates a draft GitHub release with the matching
`v<VersionPrefix>` tag. Replace the draft's generated notes with that version's `CHANGELOG.md` section, keeping the
generated **Full Changelog** link, then review the assets and publish the draft.
Drafts are not offered to update clients. Create GitHub releases only through this workflow, never by hand: it
releases only from `main`, refuses a version that already has a tag or release, and runs the full build, format, and
test checks first. The launcher's updater reads this repository's GitHub releases (`VelopackUpdateRepositoryUrl` in
`GenLauncherGO/GenLauncherGO.csproj`), so publishing a draft offers it to every installed launcher. Releases stay
portable-only.

Changes to the updater or release process also need the [update smoke test](update-smoke-test.md).

## Packaging locally

The packaging tool is a C# file-based app under `eng` that uses the repository's .NET SDK. Use `--no-cache` so the
SDK rebuilds the runner instead of reusing its temporary build cache. From the repository root, run:

```powershell
dotnet run --file ./eng/package-release.cs --no-cache
```

The script downloads the previous GitHub release so Velopack can create a delta. `--bootstrap` skips that download
and packages only a full release, which local test feeds need:

```powershell
dotnet run --file ./eng/package-release.cs --no-cache -- --bootstrap
```

Local output goes to `artifacts/release`; the script does not upload anything.
