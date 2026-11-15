# Architecture

The solution uses one application project, one test project, and test-only analyzer tooling, with no `src` folder:

| Project | Responsibility |
| --- | --- |
| `GenLauncherGO` | Windows Avalonia application with feature-local rules, side effects, workflows, and presentation |
| `GenLauncherGO.Tests` | Observable behavior, compatibility, recovery, and file-system safety tests |
| `eng/GenLauncherGO.TestAnalyzers` | Test-only analyzer enforcing the repository's test-method naming convention |

The build enforces the project-reference graph: the application and analyzer reference no projects, and tests
reference the analyzer only as analyzer tooling.

## Features

Production code is feature-first. Each feature under `GenLauncherGO/Features` keeps its rules, side-effect
implementations, and presentation together, with purposeful `Views` and resource directories. Do not add layer folders
such as `Contracts`, `Models`, `Services`, or `Support`, or any folder for file count, symmetry, or anticipated growth.
`GenLauncherGO/Shared` holds only what several features use.

| Feature | Owns |
| --- | --- |
| `Startup` | Host, setup, installation discovery, runtime contexts, and canonical launcher paths |
| `Settings` | Preferences, their persistence mapping, and the settings and executable-management screens |
| `Mods` | Content identities, catalogs, remote YAML binding, reconciliation, import, images, and mod presentation |
| `Launching` | Executable selection, arguments, launch coordination, process tracking, deployment, and recovery |
| `Integrity` | Scanning, snapshots, targets, resolution, and review |
| `Updating` | Downloads, package activity and admission, providers, and application updates |
| `Launcher` | The main window, its lists, and coordination across user workflows |

## Composition and paths

`LauncherApplicationHost.CreateServiceCollection` owns runtime composition. Bootstrap has a separate provider so it can
validate storage before runtime file logging starts.

Mutable paths carry their owning root so file operations can reject traversal and reparse-point escapes.

## Backend compatibility

GenLauncherGO consumes an external backend tied to [p0ls3r](https://github.com/p0ls3r) and the original GenLauncher
project. This repository does not control that backend, so its legacy remote YAML names and structure are preserved
exactly in the Mods transport documents and mapped once into normalized application concepts. Do not rename or reshape
that manifest contract without a deliberate compatibility plan coordinated with the backend maintainers.
