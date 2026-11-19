# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Changes in versions up to and including 1.1.1 were not recorded.

## [1.3.0] - 2026-11-19

> **Highlights**
>
> - **Native AOT.** GenLauncherGO is now compiled to native code ahead of time instead of while it runs, so it
>   starts much faster, uses less memory, and its download is about half the size.
> - **Reworked manual mod imports.** Adding a mod from files now installs a mod only once it is fully prepared,
>   explains anything it can't import, and recognizes a copy of a repository mod right away. Mods copied straight
>   into the Mods folder now appear without a restart.

### Added

- Changes you make to the Mods folder outside GenLauncherGO, such as copying in a mod, now appear when you return
  to the launcher, without a restart.
- The update dialog links to the release notes.

### Changed

- GenLauncherGO now starts faster and uses less memory, and its download is about half the size.
- A manually added mod with the name and version of a repository mod is now recognized right away instead of after
  a restart. Its files are checked against the published version the first time it launches.

### Removed

- The game name in the window title. The game switcher already shows the active game.

### Fixed

- GenLauncherGO now starts in English when Use English is selected, instead of in the Windows display language.
- Adding a mod from files now explains why it refuses a name, version or folder, or can't extract an archive. A
  failed import no longer leaves anything behind.
- Adding a mod from files can now be cancelled at its first prompt, and its details dialog no longer stays on top
  of other apps.
- Manually added mods can now have "&" in their names.
- Mods with read-only files can now be deleted.
- Patches and add-ons no longer disappear from the lists when a same-named patch or their mod's last version is
  removed.
- Download errors are now translated and say when the host no longer provides the file.

## [1.2.0] - 2026-10-25

> **Note:** GenLauncherGO now ships as a portable ZIP instead of a single executable. To upgrade, close
> GenLauncherGO, extract the ZIP into your existing launcher folder without replacing `GenLauncherGO Data`, and
> keep starting the root `GenLauncherGO.exe`.

### Added

- GenLauncherGO now updates itself. It checks for a new release at most once a day, downloads it in the
  background, and offers to restart now or later.
- Open the folder of a manually added mod, patch or add-on from its tile.
- When a game folder is protected, such as one under Program Files, GenLauncherGO asks Windows once to give
  your account write access to it.
- When Windows will only start a game client or World Builder as administrator, GenLauncherGO explains why and
  offers to restart as administrator.
- Click the version label in the main window or Launcher Settings to copy it for bug reports.

### Changed

- GenLauncherGO no longer requires administrator rights, so it no longer shows a UAC prompt at startup. Keep it
  in a folder you can write to; it now tells you when it can't write there.
- Switch between Generals and Zero Hour from the main window instead of Launcher Settings.
- Game folders are now recognized by the game's own data archives, such as `INI.big` or `INIZH.big`. A folder
  without them is rejected.
- The Launch button now leads the sidebar and shows the selected mod and game client. Windowed and Quick Start
  are checkboxes beneath it.
- Launcher Settings is reorganized. Per-game settings come first (game clients, World Builders and launch
  arguments), followed by installation paths, launcher options and troubleshooting.
- Games that start through another process are tracked more reliably.

### Removed

- The sidebar Exit button. Close the window instead.
- The setup screen's warning about game folders under Program Files.

### Fixed

- Missing or unavailable mod artwork no longer blocks or delays launching a game.
- Mod artwork no longer gets mixed up between versions with similar numbers, such as 1.0 and 1.0.1.
  Corrupted images are downloaded again, and artwork for retired versions is cleaned up. Remote mods without
  cover art now show an "image unavailable" banner instead of the user-added mod banner.
- Dialogs no longer get hidden behind other windows during startup.
- Screen readers now announce game cards, tabs, mod tiles, settings options and input fields by name instead of
  internal type names.
- Choosing an unreadable image for a mod no longer removes its current image. GenLauncherGO now says the file
  isn't a readable PNG or JPG image.
- The prompt for deleting one version of a mod now names that version.
- Your mod selection is saved when a game launches, so it is kept even if the launcher closes unexpectedly.
