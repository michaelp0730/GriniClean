# GriniClean Developer Technical Spec

This document explains GriniClean’s architecture, project layout, and the responsibilities of each major class. It’s intended for developers who want to understand the current design and extend it safely.

---

## Goals and Non-Goals

### Goals
- Provide a macOS CLI (`gc`) for safe cache discovery and trash-first cleaning.
- Keep “dangerous” operations opt-in and visible to the user.
- Provide modular structure so new functionality (e.g., malware scanning) can be added without rewriting the CLI.
- Keep OS-specific behavior behind interfaces so logic can be tested.

### Non-Goals (current scope)
- No system directory cleaning.
- No permanent deletion (Trash-first only).
- No deep “app-specific” cache semantics yet (beyond safe defaults and best-effort running-process detection).
- No background services or daemons.

---

## Repository Layout

At the solution level (`GriniClean.sln`) you currently have:

- `GriniClean.App`  
  The CLI entrypoint, Spectre.Console commands, and DI wiring.

- `GriniClean.Core`  
  Shared, cross-cutting models and enums used across modules and commands.

- `GriniClean.Infrastructure`  
  OS/file-system/process abstractions and macOS implementations.

- `GriniClean.Modules.Cache`  
  Cache scanning and cleaning module (scanner + cleaner services).

- `GriniClean.Modules.Security`  
  Placeholder for future malware scanning (ClamAV integration), not yet implemented.

---

## High-Level Architecture

GriniClean is organized in layers:

1. **App Layer (CLI):**
    - Spectre.Console commands parse flags/options and render output.
    - Commands call services through interfaces.

2. **Module Layer:**
    - Modules implement business logic for a domain area.
    - Example: Cache module contains scanning and cleaning logic.

3. **Infrastructure Layer:**
    - Abstracts OS interaction, filesystem enumeration, process detection, and trash operations.
    - Allows services/modules to be testable without macOS dependencies.

4. **Core Layer:**
    - Shared models used by the CLI + modules.

The dependency direction should be:

`App -> Modules -> Core`  
`App -> Infrastructure -> Core`  
`Modules -> Infrastructure -> Core`  
`Core` should not depend on anything else.

---

## CLI Composition (GriniClean.App)

### Program.cs responsibilities
`Program.cs` composes the application using:
- Microsoft.Extensions.DependencyInjection for DI
- Spectre.Console.Cli for command parsing and execution

#### DI registrations (current)
Infrastructure:
- `IFileSystem -> OsFileSystem`
- `IProcessService -> MacProcessService`
- `ITrashService -> MacTrashService`
- `IUserPaths -> MacUserPaths`

Cache Module:
- `ICacheScanner -> MacCacheScanner`
- `ICacheCleaner -> MacCacheCleaner`

Commands:
- `CacheScanCommand`
- `CacheCleanCommand`

Spectre wiring:
- Sets application name to `gc`
- Registers:
    - `cache-scan`
    - `cache-clean`

### TypeRegistrar / TypeResolver
Spectre.Console.Cli needs a registrar/resolve bridge. You provide:
- `TypeRegistrar(IServiceCollection)` to register services
- `TypeResolver(IServiceProvider)` to resolve services at runtime

This is intentionally minimal and keeps Spectre coupled to the same DI container used elsewhere.

---

## Core Models (GriniClean.Core)

### CacheTarget (model)
`CacheTarget` represents a cleanable/scannable cache directory (or “target”).

Expected fields (based on current usage):
- `DisplayName` (string): UI-friendly name (often directory name)
- `Path` (string): absolute path to directory
- `SizeBytes` (long?):
    - `null` when unknown (e.g., `--fast`)
    - otherwise computed directory size
- `Kind` (CacheTargetKind): identifies category (user caches root child vs container caches)
- `IsAdvanced` (bool): marks targets that are opt-in/advanced (containers)
- `IsApple` (bool): marks Apple-owned caches (hidden by default unless user opts in)

### CacheTargetKind (enum)
Used to label targets in tables and for grouping:
- `UserCachesRootChild`
- `ContainerCaches`
- (others can be added later)

### CacheScanOptions (record or class)
Options passed to `ICacheScanner.Scan`:
- `Fast` (bool): skip size calculation
- `IncludeContainers` (bool): include container caches

---

## Infrastructure (GriniClean.Infrastructure)

Infrastructure is where macOS-specific details live.

### FileSystem
#### IFileSystem
Abstracts file/directory operations so scanning can be tested and controlled.
Typical responsibilities:
- `DirectoryExists(path)`
- `FileExists(path)`
- `EnumerateDirectories(path)`
- `GetDirectorySizeBytes(path, CancellationToken)`

#### OsFileSystem
Concrete implementation using `System.IO` APIs.

#### ITrashService
Moves file or directory to Trash (trash-first policy).
- `TryMoveToTrash(string path) : string?`

Returns:
- a non-null value on success (commonly the original path or trashed path)
- null on failure

#### MacTrashService
macOS implementation. Current design uses Trash-first behavior (Finder automation and/or Homebrew `trash` depending on your current implementation).

### OS
#### IUserPaths
Provides OS-specific “known” user paths.
- `HomeDirectory` is used for safe cache roots (e.g., `~/Library/Caches`)

#### MacUserPaths
macOS implementation using `Environment.SpecialFolder.UserProfile` and/or `$HOME`.

#### IProcessService
Provides process/app running checks to prevent cleaning caches for currently-running apps.
- `IsProcessRunning(processName)`
- `IsAppRunningByBundleId(bundleId)`

#### MacProcessService
macOS implementation using a mix of:
- `pgrep` for process name checks
- AppleScript/System Events bundle-id detection for GUI apps

The “running app” detection is intentionally best-effort; targets that do not map to a bundle id or process name are not blocked.

---

## Cache Module (GriniClean.Modules.Cache)

### Interfaces
#### ICacheScanner
Scans safe cache locations and returns a list of `CacheTarget`.
- `Scan(CacheScanOptions options, CancellationToken ct) : IReadOnlyList<CacheTarget>`

#### ICacheCleaner
Moves selected cache targets to Trash and returns a summary.
- `MoveToTrash(IEnumerable<CacheTarget> targets, bool dryRun, CancellationToken ct) : CacheCleanResult`

### MacCacheScanner
Primary cache discovery logic.

#### Scan roots (current)
- User cache root:
    - `~/Library/Caches` immediate children
- Container caches (advanced, opt-in):
    - `~/Library/Containers/*/Data/Library/Caches`

#### Sorting
Returned list is sorted:
- non-advanced first
- then largest first (when size is known)
- then display name

#### Apple detection
Scanner sets `IsApple` using path-based detection:
- if directory name starts with `com.apple.`
- OR path contains `/Library/Caches/com.apple.`
- OR path contains `/Library/Containers/com.apple.`
- (optional small conservative list for non-bundle Apple names)

This flag is used by commands to exclude Apple caches by default.

### MacCacheCleaner
Responsible for the “clean” action.

Expected behavior:
- takes a list of `CacheTarget`
- for each target:
    - attempts to move directory to Trash using `ITrashService`
    - tracks successes/failures
- returns `CacheCleanResult` (requested/trashed/failed + failed paths)
- supports `dryRun` (no actual trash operations)

---

## Commands (GriniClean.App)

### CacheScanCommand
Displays cache targets in a table.

Key behaviors:
- calls `ICacheScanner.Scan(...)`
- filters results:
    - `--min-size`
    - `--show-zero`
    - `--top`
    - excludes Apple caches unless `--include-apple`
- optionally prints verbose scan diagnostics with `--verbose`
- renders:
    - Target
    - Kind (+ Apple / adv tags)
    - Size
    - Path

### CacheCleanCommand
Interactive selection + cleaning.

Key behaviors:
1. Scans and filters (same philosophy as scan):
    - excludes Apple caches unless `--include-apple`
    - applies `--min-size`, `--show-zero`, `--filter`
2. Selection modes:
    - `--mode select` (default): multi-select prompt (grouped)
    - `--mode prompt`: yes/no per item
3. Dry run:
    - `--dry-run` prints actions without moving anything
4. Confirmation prompt before executing
5. Running-app protection:
    - unless `--allow-running`, targets tied to running apps are skipped at execution time
    - this prevents cleaning caches while an app is running

---

## Safety Model

GriniClean’s safety model is enforced through:
- **Scope restriction**: scanning is limited to user-space safe roots
- **Trash-first policy**: cleaning moves items to Trash, never permanent deletion
- **Opt-in advanced features**:
    - `--include-apple` for Apple caches
    - `--include-containers` for container caches
- **Running app guard**: skips caches likely in use unless overridden

This is intentionally conservative. Defaults should remain safe even if a user runs `gc` without understanding every option.

---

## Extending GriniClean

### Adding a new command
1. Create a `*Command.cs` in `GriniClean.App`
2. Inject required services via constructor parameters
3. Register the command in DI (Program.cs)
4. Add it to `CommandApp` configuration

### Adding a new module
Recommended pattern (mirrors cache module):
- Create a project `GriniClean.Modules.<Name>`
- Define interfaces `I<Name>Scanner`, `I<Name>Service`, etc.
- Implement macOS-specific logic in the module using `Infrastructure` interfaces
- Register module services in `Program.cs`
- Add command(s) in `GriniClean.App`

### Adding new OS functionality
Add interface + macOS implementation in `GriniClean.Infrastructure.OS` or `.FileSystem`, register it in Program.cs.

---

## Testing Notes (recommended direction)

Current design is test-friendly if:
- Commands are treated as thin wrappers (they mostly are)
- Services depend on abstractions (`IFileSystem`, `ITrashService`, `IProcessService`, `IUserPaths`)

Suggested tests:
- `MacCacheScanner` with a fake `IFileSystem` to validate:
    - roots scanned
    - apple detection flags
    - container path logic
- `MacCacheCleaner` with a fake `ITrashService` to validate:
    - counts (requested/trashed/failed)
    - dry-run behavior
- `CacheCleanCommand` selection filtering logic (unit-testable if extracted into a helper/policy service later)

---

## Naming / Conventions

- CLI name: `gc`
- Project/solution name: `GriniClean`
- Avoid touching any path outside `~/Library/...`
- Avoid adding “silent” destructive behavior; keep prompts and opt-in flags explicit
