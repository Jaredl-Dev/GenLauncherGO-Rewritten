# Architecture

The solution uses one application project, one test project, and test-only analyzer tooling, with no `src` folder:

| Project | Responsibility |
| --- | --- |
| `GenLauncherGO` | Windows Avalonia application with feature-local rules, side effects, workflows, and presentation |
| `GenLauncherGO.Tests` | Observable behavior, compatibility, recovery, and file-system safety tests |
| `eng/GenLauncherGO.TestAnalyzers` | Test-only analyzer enforcing the repository's test-method naming convention |

Production code lives under `GenLauncherGO/Features` in `Startup`, `Settings`, `Mods`, `Launching`, `Integrity`,
`Updating`, and `Launcher`. Rules, implementations, and UI services live beside the workflows they serve; views and
resources retain purposeful directories. `GenLauncherGO/Shared` holds only what several features use. Tests follow
the same feature groups under `GenLauncherGO.Tests/Features`, with reusable test helpers under
`GenLauncherGO.Tests/Testing` and naming checks under `GenLauncherGO.Tests/Conventions`.

The build enforces the project-reference graph: the application and analyzer reference no projects, and tests
reference the analyzer only as analyzer tooling. Interfaces represent actual side-effect or external boundaries; other
code normally uses internal sealed types.

`LauncherApplicationHost.CreateServiceCollection` owns runtime composition. Bootstrap has a separate provider so it can
validate storage before runtime file logging starts; both stages use one preferences-registration helper.

Mutable paths carry their owning root so file operations can reject traversal and reparse-point escapes.

## Backend compatibility

GenLauncherGO consumes an external backend tied to [p0ls3r](https://github.com/p0ls3r) and the original GenLauncher
project. This repository does not control that backend, so its legacy remote YAML names and structure are preserved
exactly in the Mods transport documents and mapped once into normalized application concepts. Do not rename or reshape
that manifest contract without a deliberate compatibility plan coordinated with the backend maintainers.
