# Tammi

A task-first software development workspace management tool.

Pick the tasks you're working on (from GitHub or JIRA), and Tammi sets up a workspace around them.
The workspace holds the relevant git repositories, shows their status, runs AI agents on the work and
opens everything in your other tools.

See [docs/CONTEXT.md](docs/CONTEXT.md) for the full product context.

## Installing

Download the installer for your OS and architecture (`tammi-installer-<tag>-<linux-x64|linux-arm64|win-x64>`) from the
[latest release](https://github.com/deosrc/Tammi/releases/latest) and run it. It needs no admin rights: it checks
for [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) and `git` (telling you how to install what's
missing), downloads the matching release to its own folder in your user app data folder, and adds Tammi to your
application menu (a `.desktop` file on Linux, a Start menu shortcut on Windows).

```shell
tammi-installer                  # the latest release
tammi-installer --prerelease     # the latest release, even if it's a pre-release
tammi-installer --tag v0.2.0     # a particular release
tammi-installer --self-contained # the build that bundles .NET, so it needn't be installed
```

Each version is kept in its own folder: `%LOCALAPPDATA%\Tammi\Versions\<version>` on Windows and
`~/.local/share/tammi/versions/<version>` on Linux (under `$XDG_DATA_HOME`).

## Building and running

The UI is a set of Blazor components in a shared Razor class library (`src/Deosrc.Tammi.UI`), shown by a native host
for each platform:

| Project | Purpose |
|---------|---------|
| `src/Deosrc.Tammi.Core` | Domain models and services (no UI dependencies) |
| `src/Deosrc.Tammi.UI` | Shared Blazor UI |
| `src/Deosrc.Tammi.Windows` | Windows host: .NET MAUI Blazor Hybrid (`BlazorWebView`) |
| `src/Deosrc.Tammi.Linux` | Linux host: Photino.Blazor |
| `src/Deosrc.Tammi.Installation` | Downloads, installs and cleans up released versions (shared by the installer and the updater) |
| `src/Deosrc.Tammi.Installer` | The installer app, one codebase for every OS |
| `tests/Deosrc.Tammi.Core.Tests` | xUnit tests |

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`)
- `git`
- **Windows:** the MAUI workload (`dotnet workload install maui-windows`) and the
  [WebView2 runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (included with Windows 11)
- **Linux:** GTK 3 and WebKitGTK 4.1 (e.g. `sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0` on Debian/Ubuntu),
  and glibc 2.29 or later for the embedded terminal

### Windows

```shell
dotnet build Deosrc.Tammi.slnx
dotnet test
dotnet run --project src/Deosrc.Tammi.Windows
```

### Linux

The Windows host can't be built on Linux, so use the `Deosrc.Tammi.Linux.slnf` solution filter, which leaves it out.

```shell
dotnet build Deosrc.Tammi.Linux.slnf
dotnet test --project tests/Deosrc.Tammi.Core.Tests
dotnet run --project src/Deosrc.Tammi.Linux
```

#### Trying a branch in the running app

`scripts/switch-linux-instance.sh` builds this checkout and replaces the running Tammi with it, which suits trying a
change (or an agent's branch) in the real app. It starts a detached launcher that waits for the running Tammi to exit,
so close Tammi when you're ready: that ends all its agents, including one that ran the script. Pass `--stop` to have
the script ask the running Tammi to exit instead, which skips its close warning. The new instance's output goes to
`tammi-switch.log` in the temp directory.

`dotnet test` runs on Microsoft.Testing.Platform (set in `global.json`). It evaluates every project in a solution or
solution filter, including the Windows host, so on Linux run it against the test project instead.
