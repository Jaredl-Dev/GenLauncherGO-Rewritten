# Reference

## Supported game clients

The following game clients are automatically detected in the configured game folder:

| Game | Client | Executable |
| --- | --- | --- |
| Generals or Zero Hour | Retail | `generals.exe` |
| Generals | TheSuperHackers | `generalsv.exe` |
| Zero Hour | TheSuperHackers | `generalszh.exe` |
| Zero Hour | GeneralsOnline | `generalsonlinezh.exe` |

## Supported World Builders

World Builder support is optional.

| World Builder | Executable |
| --- | --- |
| Original World Builder | `WorldBuilder.exe` |
| TheSuperHackers Generals | `worldbuilderv.exe` |
| TheSuperHackers Zero Hour | `worldbuilderzh.exe` |

## Custom executables

Custom game client and World Builder executables can be registered under **Launcher Settings > Executables**, with
**Manage game clients** or **Manage World Builders**.

## Same drive as your games

When GenLauncherGO and the game are on the same NTFS drive, each launch hard-links the selected content's files into
the game folder, which is nearly instant and uses no extra disk space. On a different drive, or on one that doesn't
support hard links (such as FAT32 or exFAT, common on USB drives), it copies every file at each launch instead. That
takes longer for large mods and needs enough free space for a temporary second copy.

## Launcher folder layout

The portable package keeps its update machinery beside the root launcher, and the launcher creates one user-owned
`GenLauncherGO Data` folder there when it starts. In the table below, `<game>` is either `C&C Generals Data` or
`C&C Zero Hour Data`, and `<name>` identifies a content entry.

| Path | Purpose |
| --- | --- |
| `GenLauncherGO.exe` | Stable portable launcher entry point |
| `Update.exe` | Portable update host |
| `.portable` | Marker that prevents installer registration and shortcuts |
| `current\GenLauncherGO.exe` | Current application payload |
| `current\*.dll` | Native rendering and text-shaping libraries used by the application |
| `current\LICENSE` | GPLv3 license text supplied with the application |
| `current\sq.version` | Installed package identity and version |
| `packages` | Downloaded update packages managed by Velopack |
| `GenLauncherGO Data\LauncherPreferences.yaml` | Launcher settings |
| `GenLauncherGO Data\LastApplicationUpdateCheckUtc.txt` | Time of the last automatic update check |
| `GenLauncherGO Data\<game>\Mods` | Installed mods, patches, and add-ons |
| `GenLauncherGO Data\<game>\Runtime\Cache\Images\<name>` | Downloaded or user-selected images |
| `GenLauncherGO Data\<game>\Runtime\Deployment` | Deployment manifests, journals, locks, and recovery backups |
| `GenLauncherGO Data\<game>\Runtime\Integrity` | Managed-content integrity snapshots |
| `GenLauncherGO Data\<game>\Runtime\State\LauncherData.yaml` | Local catalog state and installed-content metadata |
| `GenLauncherGO Data\<game>\Runtime\State\PackageBackups` | Previous copy of a package while a download replaces it, restored if the replacement is interrupted |
| `GenLauncherGO Data\<game>\Runtime\Temp` | Temporary download and installation staging, cleared on startup except partly downloaded packages, which a paused download resumes from |
| `GenLauncherGO Data\Logs` | Rolling diagnostic logs |
