# Portable update smoke test

[Required quality gates](../../CONTRIBUTING.md#required-quality-gates) says when to run this manual two-version test.
It exercises the same Velopack packages as production while serving the feed only from the local machine, and
covers what only a real update can prove: the package is found, downloaded, and applied, and `GenLauncherGO Data`
survives. The dialog's timing, including waiting for active work and **Later**, is covered by
`LauncherApplicationUpdateCoordinatorTests`.

You need a Generals or Zero Hour installation for first-run setup, because the launcher checks for updates only after
startup completes.

## Prepare two local versions

1. Work on a disposable branch or copy of the repository. Temporarily replace the `GithubSource` constructed in
   `VelopackLauncherApplicationUpdateService` with
   `new SimpleWebSource("http://127.0.0.1:8123")`. Do not commit this test-only edit.
2. Set `VersionPrefix` in `GenLauncherGO/GenLauncherGO.csproj` to a lower numeric test version such as
   `1.1.101`, then package it with `--bootstrap` as described in [Packaging locally](releasing.md#packaging-locally).
3. Copy `artifacts/release/GenLauncherGO-win-Portable.zip` to a temporary `version-a` directory.
4. Set `VersionPrefix` to a higher version such as `1.1.102` and package it the same way. Leave this higher-version
   feed in `artifacts/release`.
5. In a separate terminal at the repository root, serve `artifacts/release` at the address from step 1 and leave it
   running. The script listens on that address and needs only the .NET SDK:

   ```powershell
   dotnet run --file ./eng/serve-update-feed.cs --no-cache
   ```

## Apply the update

1. Extract `version-a\GenLauncherGO-win-Portable.zip` to a new `installed` directory. In it, create
   `GenLauncherGO Data\update-smoke-sentinel.txt` and put recognizable text in it.
2. Run the root `installed\GenLauncherGO.exe` and complete first-run setup.
3. Keep the launcher window focused: the update-ready dialog appears only while the launcher is the active window.
   Wait for the dialog, choose **Restart now**, and allow the old process to exit.
4. Confirm GenLauncherGO restarts and `installed\current\sq.version` identifies the higher test version.
5. Confirm `GenLauncherGO Data\update-smoke-sentinel.txt` still exists with unchanged contents.

To rerun, start again from a fresh extraction of the `version-a` archive. Deleting only
`GenLauncherGO Data\LastApplicationUpdateCheckUtc.txt` resets the 24-hour automatic-update throttle but does not clear
a previously downloaded update, so a reused installation can skip the check and download this test must exercise.

## Clean up

Close the launcher, stop the feed server with Ctrl+C, revert the temporary source and `VersionPrefix` edits, and
remove the disposable test directories.
