# TAMMi

A task-first software development workspace management tool.

Pick the tasks you're working on (from GitHub or JIRA), and TAMMi sets up a workspace around them.
The workspace holds the relevant git repositories, shows their status, runs AI agents on the work and
opens everything in your other tools.

See [docs/CONTEXT.md](docs/CONTEXT.md) for the full product context.

## Building and running

The UI is a set of Blazor components in a shared Razor class library (`src/TAMMi.UI`), shown by a native host
for each platform:

| Project | Purpose |
|---------|---------|
| `src/TAMMi.Core` | Domain models and services (no UI dependencies) |
| `src/TAMMi.UI` | Shared Blazor UI |
| `src/TAMMi.Windows` | Windows host: .NET MAUI Blazor Hybrid (`BlazorWebView`) |
| `src/TAMMi.Linux` | Linux host: Photino.Blazor |
| `tests/TAMMi.Core.Tests` | xUnit tests |

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`)
- `git`
- **Windows:** the MAUI workload (`dotnet workload install maui-windows`) and the
  [WebView2 runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (included with Windows 11)
- **Linux:** GTK 3 and WebKitGTK 4.1 (e.g. `sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0` on Debian/Ubuntu),
  and glibc 2.29 or later for the embedded terminal

### Windows

```shell
dotnet build TAMMi.slnx
dotnet test
dotnet run --project src/TAMMi.Windows
```

### Linux

The Windows host can't be built on Linux, so use the `TAMMi.Linux.slnf` solution filter, which leaves it out.

```shell
dotnet build TAMMi.Linux.slnf
dotnet test --project tests/TAMMi.Core.Tests
dotnet run --project src/TAMMi.Linux
```

`dotnet test` runs on Microsoft.Testing.Platform (set in `global.json`). It evaluates every project in a solution or
solution filter, including the Windows host, so on Linux run it against the test project instead.

### Developer pages

Settings → About links to pages for trying parts of the app on their own, such as **Terminal**, which runs the platform's default
shell in the embedded terminal.
