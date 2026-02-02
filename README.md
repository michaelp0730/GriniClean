# GriniClean

GriniClean is a **safe, trash-first command-line cache cleaner for macOS**, inspired by tools like CleanMyMac and CCleaner, but designed to be **transparent, scriptable, and respectful of your system**.

GriniClean:
- Only operates in **user-space** (`~/Library`)
- **Never touches system directories**
- Moves files to **Trash by default** (not permanent delete)
- Clearly labels **Apple vs third-party** caches
- Prevents cleaning caches for **currently running apps**
- Is fully open source and modular

---

## ⚠️ Safety Principles (Read This First)

GriniClean is intentionally conservative.

By default:
- ❌ No system directories are scanned or modified
- ❌ Apple caches (`com.apple.*`) are excluded
- ❌ Sandbox container caches are excluded
- ❌ Caches for currently running apps are skipped
- ✅ Everything is moved to **Trash**, not deleted

You must **explicitly opt in** to anything advanced.

---

## Requirements

You will need the following installed on your Mac:

### 1) Git
Used to clone the repository.

If you don’t already have Git, install Apple’s Command Line Tools:

```bash
xcode-select --install
```
More info:
https://developer.apple.com/documentation/xcode/installing-the-command-line-tools/

### 2) Homebrew

Used to install dependencies.

Install Homebrew (if not already installed):
```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

Verify:
```bash
brew --version
```

### 3) Required Homebrew Packages
**Install the trash CLI (recommended)**

This is used to reliably move files to the macOS Trash.
```bash
brew install trash
```

**Install .NET 8**

GriniClean targets .NET 8.

```bash
brew install dotnet@8
```

Verify:
```bash
dotnet --version
```

You should see 8.x.x.

---

## Installation

Clone the repository:
```bash
git clone https://github.com/michaelp0730/GriniClean.git
cd GriniClean
```

Build and run:
```bash
dotnet run --project GriniClean.App -- --help
```

You should see available commands like:
```bash
COMMANDS:
  cache-scan     Scan user cache locations
  cache-clean    Select and clean caches (trash-first)
```

---

## Usage Overview

GriniClean uses the command name:

```bash
gc
```

When running via `dotnet run`, commands look like:
```bash
dotnet run --project GriniClean.App -- <command> [options]
```

---

## 📋 Listing Caches (cache-scan)

### Basic scan (recommended)

Lists third-party user caches only, larger than 1MB.

```bash
dotnet run --project GriniClean.App -- cache-scan
```

### Show caches larger than a specific size

```bash
dotnet run --project GriniClean.App -- cache-scan --min-size 50MB
```

Supported units:
- KB
- MB
- GB
- TB

### Include Apple user caches (advanced)
```bash
dotnet run --project GriniClean.App -- cache-scan --include-apple
```

Apple caches are clearly labeled:
```bash
User cache (Apple)
Container cache (adv) (Apple)
```

### Include sandbox container caches (advanced)
```bash
dotnet run --project GriniClean.App -- cache-scan --include-containers
```

These live under:
```bash
~/Library/Containers/*/Data/Library/Caches
```

### Include both Apple + container caches
```bash
dotnet run --project GriniClean.App -- cache-scan --include-apple --include-containers
```

### Fast mode (skip size calculation)
```bash
dotnet run --project GriniClean.App -- cache-scan --fast
```
Useful if you just want a quick inventory.

### Verbose diagnostics
```bash
dotnet run --project GriniClean.App -- cache-scan --verbose
```
Shows exactly which directories are scanned.

---

## 🧹 Cleaning Caches (cache-clean)

### Dry run (strongly recommended first)
```bash
dotnet run --project GriniClean.App -- cache-clean --dry-run
```
Nothing is moved. You’ll see exactly what ***would*** happen.

### Interactive multi-select (default)
```bash
dotnet run --project GriniClean.App -- cache-clean
```

- Use Space to select
- Enter to confirm
- Caches are grouped (user vs containers)
- Apple caches are hidden unless explicitly enabled

### Yes/No per item mode
```bash
dotnet run --project GriniClean.App -- cache-clean --mode prompt
```
You’ll be asked about each cache individually.

### Clean only large caches
```bash
dotnet run --project GriniClean.App -- cache-clean --min-size 500MB
```

### Filter by name or path
```bash
dotnet run --project GriniClean.App -- cache-clean --filter chrome
```

Matches against:

- Display name
- Full path

Case-insensitive.

### Include Apple caches (advanced)
```bash
dotnet run --project GriniClean.App -- cache-clean --include-apple
```

### Include container caches (advanced)
```bash
dotnet run --project GriniClean.App -- cache-clean --include-containers
```

### Allow cleaning caches for running apps (not recommended)
By default, GriniClean skips caches belonging to apps that appear to be running.

To override:
```bash
dotnet run --project GriniClean.App -- cache-clean --allow-running
```
You will still be prompted to confirm.

---

## What Happens When You Clean?

- Selected caches are moved to macOS Trash
- Nothing is permanently deleted
- If something goes wrong, you can restore it from Trash

## Troubleshooting

### “Failed to move to Trash” but files appear in Trash
This can happen due to Finder / AppleScript exit codes.

If you installed `trash` via Homebrew, GriniClean will still succeed safely.
The summary reflects ___requested vs confirmed___, not permanent deletion.

## Permission prompts
macOS may ask for permission to allow automation (Finder / System Events).
This is expected and safe.

---

## License
Licensed under the GNU General Public License v3.0 (GPL-3.0).

This means:
- Free and open source
- Modifications must remain open source
- No one may legally monetize GriniClean or derivatives

See the `LICENSE` file for full details.
