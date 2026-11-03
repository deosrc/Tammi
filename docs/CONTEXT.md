# TAMMi: Product Context

> Living document. This is the source of truth for what TAMMi is and the words we use for it.
> Update it as decisions are made. Unresolved items live under [Open Questions](#open-questions).

## Purpose

TAMMi is a **software development workspace management tool** with a **task-first** approach.

The developer starts from the work to be done (one or more tasks), not from a repository. TAMMi
then builds a workspace around those tasks: a named folder holding the relevant git repositories,
with a view of their git state, AI agents working on the tasks, and shortcuts into other tools.

## Principles

- **TAMMi never requires changes to repositories.** It works without adding files, config or commits
  to any repository. Its own files live in the workspace's `.tammi/` folder and in app data.

## Glossary

| Term | Meaning |
|------|---------|
| **Task** | A unit of work from an integration (e.g. a GitHub issue or JIRA ticket). TAMMi doesn't support manually defined tasks: a workspace without tasks is described by its name. |
| **Task Source** | The integration a task comes from. |
| **Workspace** | A named collection of tasks and repositories in a folder on disk. It is the main unit of work in TAMMi. |
| **Workspace Folder** | The workspace's location on disk: `<parent folder>/<workspace name>`. |
| **Repository** | A git repository that can be added to a workspace. |
| **Repository Catalogue** | The app-level list of known repositories, collected from all repository sources. |
| **Repository Source** | Where catalogue entries come from: an integration (e.g. GitHub), a local repository added manually, or a **Monitored Folder**. |
| **Monitored Folder** | A local folder (where repositories are usually cloned) that TAMMi scans for git repositories. |
| **Local Repository** | A catalogue repository with a clone on disk, outside any workspace. Workspaces create their worktrees from it. |
| **Remote-only Repository** | A catalogue repository known only through an integration, with no clone on disk yet. It must be cloned, which makes it a Local Repository, before it can be added to a workspace. |
| **Workspace Worktree** | A `git worktree` of a Local Repository, created inside the Workspace Folder. This is how a repository is included in a workspace. |
| **Integration** | A connection to an external service (GitHub, JIRA, …). It can provide tasks, repositories or both. |
| **Agent** | An AI coding agent session that runs in the context of a workspace. It can be started, interacted with and stopped. |
| **Agent Definition** | A configured agent CLI (e.g. Claude Code) that TAMMi can start: name, command, arguments, how to pass the initial prompt, and how to resume a session. |
| **Supported Agent** | An agent CLI that TAMMi knows how to start, with a built-in Agent Definition (e.g. Claude Code). It's always offered and isn't configured by the user. |
| **Custom Agent** | An agent CLI the user sets up with their own Agent Definition (e.g. Codex CLI or Gemini CLI). |
| **Open in…** | Actions that open the workspace or a repository in an external tool (IDE, terminal, file manager, …). |

## Core Flow: Creating a Workspace

1. **Choose tasks.** The user picks tasks first, because they drive the rest of the setup.
   - Tasks come from integrations (GitHub, JIRA).
   - Tasks are optional. A workspace with no tasks is described by its name alone.
   - One workspace can mix tasks from any sources.
   - TAMMi only reads tasks from integrations. It doesn't write back status changes or PR links yet.
   - The task list can be changed after the workspace is created.
2. **Name and locate the workspace.**
   - The name defaults to the task name when exactly one task is selected. Otherwise the user enters it.
   - The parent folder defaults to the **Default Workspace Parent Folder** app setting.
3. **Select repositories** from the Repository Catalogue.
   - **Local Repository:** TAMMi creates a Workspace Worktree for it in the Workspace Folder.
   - **Remote-only Repository:** TAMMi first guides the user through cloning it to a local folder.
     This turns it into a Local Repository, and TAMMi then creates the worktree as normal. The clone
     lives outside the workspace, so other workspaces can reuse it.
   - The clone defaults to `<Monitored Folder>/<repo>`, so TAMMi finds it automatically. If there are
     several Monitored Folders, the user picks one. The user can choose any other folder instead, and
     the clone is then added as a manually added local repository.

### Repositories on disk

```
<clone folder>/<repo>/              Local Repository (shared, outside any workspace)
<parent folder>/<workspace name>/
    .tammi/                         Workspace metadata (tasks, repositories, branches, settings)
                                    and the task context file for agents
    <repo>/                         Workspace Worktree of <clone folder>/<repo>
    <another repo>/                 Workspace Worktree of another Local Repository
```

Workspaces never hold full clones. Each repository in a workspace is a worktree of a Local Repository,
so several workspaces can work on the same repository at once without cloning it again.

**Matching clones to integration repositories.** A local clone and an integration repository are the
same catalogue entry when any of the clone's remotes matches the integration repository's URL. URLs are
normalised before comparing, so HTTPS and SSH forms match. A clone of a fork with an `upstream` remote
therefore counts as a Local Repository for both the fork and the upstream repository.

**Moved or deleted Local Repositories.** TAMMi detects when a Workspace Worktree's Local Repository
has been moved or deleted, and marks the workspace as broken on Home and in the Workspace View. It
offers to repair the worktree: either point it at the repository's new location (`git worktree repair`)
or clone the repository again. `git worktree repair` doesn't fix the worktree's submodules, whose links to their
git data are relative paths, so TAMMi relinks them itself. Moving a Workspace Folder breaks its worktrees in the
same way, and is repaired the same way.

**Metadata.** Each workspace describes itself in `.tammi/workspace.json`: an ID that never changes, its name, its
tasks (source and key) and, for each repository, the worktree's folder name, the Local Repository's path, the branch
and the base it was created from. The file doesn't hold the Workspace Folder's own path, so a moved workspace still
reads correctly. The app's data folder keeps only an index of known workspaces (`workspaces.json`: each one's ID,
name and folder), which Home uses to list workspaces.
- Both files are versioned and migrated like `settings.json`.
- A workspace whose folder or metadata is missing (moved or deleted) is still listed, by its last known name. If it was
  moved, the user can add it again from its new folder, which updates its index entry. A workspace whose metadata
  can't be read is listed as unreadable.
- If the index can't be read, no workspaces are listed and TAMMi keeps a copy of it as `workspaces.invalid.json`.
- A repository's folder name must be a single folder name, never a path, so nothing TAMMi does to a worktree can reach
  outside the Workspace Folder.

**Folder names.** A Workspace Worktree's folder defaults to the repository name (e.g. `api`). If two
repositories in the workspace have the same name, TAMMi prefixes the owner (e.g. `acme-api`). The user
can change any folder name during setup.

**Branches.** Git won't check out one branch in two worktrees, so each Workspace Worktree gets its
own branch. It is created automatically when the workspace is created:
- TAMMi suggests a branch name from the task, e.g. `<task-key>-<slug>`. When there are no tasks or
  several tasks, it uses the workspace name instead.
- The user can edit the suggested name before the workspace is created.
- The same branch name is used in every repository in the workspace.
- **Base:** TAMMi fetches each Local Repository, then branches from the remote's default branch
  (e.g. `origin/main`). The user can choose a different base branch for each repository.
- The new branch doesn't track its base, so pushing it publishes it under its own name rather than to the base.

**Submodules.** When a Workspace Worktree is created, TAMMi checks out its submodules automatically
(`git submodule update --init --recursive`).

**Deleting a workspace.**
1. If any worktree has uncommitted changes or unpushed commits (including inside its submodules), TAMMi lists them
   and asks the user to confirm. A worktree whose Local Repository is missing can't be checked, so it needs
   confirming too.
2. TAMMi removes each Workspace Worktree (`git worktree remove`) and then the Workspace Folder.
3. TAMMi offers to delete the workspace branches from the Local Repositories. Branches that are merged
   or pushed are selected for deletion by default. Others are kept unless the user selects them.
   - **Merged** means the branch's tip is reachable from its base branch (e.g. `origin/main`). Squash and rebase
     merges aren't detected.
   - **Pushed** means every commit on the branch is on a remote-tracking branch, as for unpushed commits on Home.

## Main Screens

### Home

- Lists existing workspaces with a status summary for each, so the user can see what needs
  attention without opening the workspace:
  - **Working directory changes** across the workspace's repositories (e.g. uncommitted changes)
  - **Agent status** for the workspace's AI agents (see [agent states](#agents))
- Each workspace shows a compact summary line (e.g. "2/3 repos changed · 1 agent needs input"). It can
  be expanded to show a breakdown for each repository and agent.
- A repository counts as **changed** when it has uncommitted changes (staged, unstaged or untracked)
  or unpushed commits, i.e. work that could be lost. Being **behind** the remote is shown separately,
  for information, and doesn't count as changed.
  - **Unpushed commits** are commits on HEAD that aren't on any remote-tracking branch. This doesn't depend on
    the upstream, so it works for new workspace branches (which track the base branch, or nothing) and for
    submodules (which usually have a detached HEAD). A repository with no remotes has no unpushed commits.
  - Changes and unpushed commits inside a submodule make its parent repository changed too.
- **Keeping git status current:**
  - Workspaces open in a tab watch their worktrees for file changes and update live.
  - Other workspaces refresh when Home is shown, and on a timer (interval in Settings).
  - Agent status is always live, because TAMMi runs the agent processes itself.
- Opening a workspace shows it in a **tab**. Several workspaces can be open at once.
- Starts the Create Workspace flow.

### Workspace View (tab)

- **Side panel** (in this order, top to bottom)
  - Running agents
  - Git status of each repository in the workspace
  - "Open in…" tools
- **Git operations**, including a git graph view
- **Agents**: start, interact with and stop AI agents (see [Agents](#agents))
- **Terminals**: any number of embedded terminals, shown as tabs alongside agent sessions. A new
  terminal opens in the workspace folder by default, or the user can open one in a specific repository.
  The shell defaults to PowerShell on Windows (`pwsh` if installed) and `$SHELL` on Linux, and can be
  changed in Settings.
  - **Copy and paste:** Ctrl+Shift+C and Ctrl+Shift+V on every platform. On Windows, as in Windows Terminal,
    Ctrl+C also copies when text is selected (otherwise it interrupts the process) and Ctrl+V pastes.

### Settings

Settings are grouped into sections, listed in a vertical tab list down the left of the screen:

- **General**
  - Default Workspace Parent Folder (defaults to `Workspaces` in the user's home folder)
- **Integrations:** the connected integrations, each with its connection status, in alphabetical order
  - A single Add button opens a menu of the integration types (GitHub, JIRA), in alphabetical order.
  - The user adds GitHub (github.com or a GitHub Enterprise Server) and JIRA sites, authenticates them again with a new
    token, tests them and removes them.
  - TAMMi checks a token with the service before saving it, so a rejected token is never saved and an integration is
    never added without working credentials.
  - Each integration's status is checked when the section opens and on demand: connected (with the account's name),
    not authenticated (no token saved), authentication failed (the token was rejected), or couldn't connect (with the
    reason, e.g. the service can't be reached or there's no credential store).
  - Removing an integration deletes its token. An integration's ID comes from the service it connects to (`github`
    for github.com, `github-<host>` for an Enterprise Server, `jira-<host>[-<path>]` for a JIRA site), so connecting
    the same service again gives the same ID, and workspace tasks from it find their source again. The same service
    can't be connected twice.
- **Git**
  - Monitored Folder(s)
  - Manually added local repositories
  - Status refresh interval for workspaces that aren't open (whole minutes from 1 to 1440, default 5)
  - The versions of the git CLI and of libgit2 (used through LibGit2Sharp) that TAMMi uses
- **Agents**
  - **Supported Agents** are always listed, and can't be edited or removed. Each one shows whether its CLI is ready:
    TAMMi finds the command and runs it to get its version (e.g. `claude --version`). If that fails, it says why
    (not found, failed with an error, or didn't finish) and links to the agent's install guide. The user can check
    again after installing it.
  - **Custom Agents** are added, edited, reordered and removed by the user. Each one has a name, command, arguments
    and initial prompt arguments, and shows the command line it starts with. Each agent needs its own name, ignoring
    case and surrounding spaces, including the Supported Agents' names. A name that's already used is still saved,
    but shown as an error until it's changed.
  - Agents are offered in that order: the Supported Agents, then the Custom Agents.
  - The initial prompt template. Clearing it restores the default.
- **Tools**
  - The external tools offered under "Open in…"
  - Terminal shell: executable and arguments. Leaving the executable empty uses the platform shell, with its own
    arguments. Changes apply to terminals started afterwards.

Lists of arguments (for the terminal shell and Custom Agents) are edited one argument per line, so they need no
quoting. Blank lines are ignored.
- **About:** the TAMMi version, and the git CLI and libgit2 versions

Changes are saved as soon as they're made. The settings live in `settings.json` in the app's Config folder (see
[Technical Constraints](#technical-constraints)):
- The file has a `version`. When its layout changes, the version goes up and TAMMi migrates older files as it
  loads them. A file from a newer TAMMi is read as far as possible.
- Missing settings take their default values, and out-of-range values are brought into range.
- If the file can't be read, TAMMi uses the defaults, says so on the Settings screen, and keeps a copy of the file
  as `settings.invalid.json` so the next save doesn't lose it.

## Git

Operations available in the Workspace View:
- Status and the git graph
- Fetch, pull and push
- Stage, unstage and commit
- View diffs

**Submodules are supported** by all of these. Submodules appear in status, and the operations can be
applied inside them. Creating and switching branches, stashing, merging, rebasing and history rewrites
are left to other tools for now.

## Open in… Tools

- TAMMi has built-in definitions for common tools: VS Code, Visual Studio (Windows only), JetBrains
  Rider, the system file manager and the system terminal.
- Only built-in tools detected as installed are shown.
- Users can add their own tools.
- Each tool has: **name**, **executable**, **arguments** (with a `{path}` placeholder for the folder
  being opened), **enabled/disabled** and **scope**.
- **Scope** sets what a tool can open: the workspace folder, a single repository, or both. Workspace
  tools appear in the side panel's tools section. Repository tools appear on each repository's entry
  in the git status section.
- **Visual Studio** opens solutions, not folders. TAMMi finds the `.sln` and `.slnx` files in the
  workspace's repositories and shows an "Open in…" entry for each one (e.g. "Visual Studio: Api.sln").

## Agents

- Agents run as **CLI processes inside the embedded terminal component**. The user interacts with them
  through the terminal, just as they would outside TAMMi.
- Agents are **pluggable**. Each Agent Definition has a name, a command and arguments. Supported Agents have
  built-in definitions; Custom Agents are configured much like an "Open in…" tool.
- **Working directory:** an agent starts in the Workspace Folder by default, so it can see and change
  every repository. The user can start it in a single Workspace Worktree instead.
- **Task context:** TAMMi writes the workspace's task details (title, description, links) to a markdown
  context file in `.tammi/` and keeps it up to date. When an agent starts, TAMMi sends it an
  **initial prompt** telling it about the context file. The initial prompt is configurable, with a
  placeholder for the context file's path. Each Agent Definition says how the prompt is passed to its CLI.
  - The template's placeholder is `{contextFile}`, replaced with the file's full path. The rest of the template is
    sent as it is.
  - An Agent Definition has two lists of arguments. Its **arguments** are passed every time it starts. Its
    **initial prompt arguments** are added after them only when there's a prompt to send, with `{prompt}` where the
    prompt goes: `{prompt}` alone passes it as a positional argument, and `--prompt` then `{prompt}` as an option's
    value, so the option is left out with the prompt. A definition without initial prompt arguments doesn't take an
    initial prompt, and starts without one. Claude Code takes it as its positional argument.
  - The prompt is always passed as one argument, exactly as written. On Windows, a CLI that's a batch file (e.g.
    `claude.cmd`, installed with npm) runs through `cmd.exe`, so TAMMi also escapes `cmd.exe`'s special characters.
    `cmd.exe` can't pass on line breaks, so a template with several lines can't be sent to a batch file: starting
    the agent fails and says why. The default template is a single line.
- **Claude Code** is the first Supported Agent. Others (e.g. Codex CLI, Gemini CLI) can be added as Custom Agents,
  and may become Supported Agents later.
- **Status** comes from the process (running, exited and exit code). Where an agent offers hooks or
  status events (e.g. Claude Code hooks), TAMMi uses them for richer states. In keeping with the
  principles, hooks are supplied when TAMMi launches the agent (e.g. through CLI arguments), never by
  changing repository files.
- **States**, shown on Home and in the Workspace View:

  | State | Meaning | Needs hooks |
  |-------|---------|-------------|
  | Working | The agent is busy | Yes |
  | Needs input | The agent is waiting on the user (e.g. a question or permission prompt) | Yes |
  | Idle | The agent has finished its turn | Yes |
  | Running | The process is running, but finer detail isn't available | No |
  | Exited | The process ended normally, or the user stopped it | No |
  | Error | The process crashed, was killed by something other than TAMMi, or exited with a non-zero code | No |

  Agents with hooks use Working / Needs input / Idle while running. Agents without hooks show Running.
  Stopping an agent hangs up its terminal and kills the process if it doesn't exit, so the exit code it then
  gets doesn't mean it failed: a stopped agent is Exited, whatever its exit code.
- **Lifetime:**
  - Closing a workspace tab doesn't stop its terminals or agents. They keep running in the background,
    their status still shows on Home, and reopening the tab reattaches to them.
  - Closing TAMMi warns if any agents are running, then ends all terminal and agent processes.
  - **Agent sessions can be resumed across app restarts.** TAMMi records each agent session (e.g. its
    session ID) in `.tammi/`. Later, the user can return to it using the agent's own resume command
    (e.g. `claude --resume <session-id>`). Each Agent Definition says how to resume a session and how
    TAMMi gets the session ID (e.g. by setting it at launch, or reading it from a hook).
  - Plain terminals aren't restored after a restart.

## Integrations

### GitHub

- Provides **tasks** (issues) and **repositories**. It works with github.com and with GitHub Enterprise Server,
  given its API URL.
- **Authentication:** a personal access token (fine-grained, or classic with the `repo` scope for private
  repositories), kept in the credential store. Fine-grained tokens need read access to issues and metadata.
- **Task keys** are GitHub's issue references, `owner/repo#number`. Pull requests aren't tasks.
- **Finding tasks:** with no query, TAMMi lists the user's open issues (`is:open assignee:@me archived:false`),
  most recently updated first. A query uses GitHub's issue search syntax. A task key or an issue's URL finds that
  issue.
- **Repositories** are those the user owns, collaborates on or reaches through an organisation. Each has its HTTPS and
  SSH clone URLs.
- **Rate limits:** when GitHub asks TAMMi to wait a few seconds, it waits and retries. For longer waits, the request
  fails and says when to try again.

### JIRA

- Provides **tasks** (issues). It works with JIRA Cloud and with JIRA Server and Data Center, given the site's URL.
  Each JIRA site is a separate integration.
- **Authentication:**
  - **JIRA Cloud:** the Atlassian account's email address (a setting) and an API token (kept in the credential store).
    API tokens with scopes only work through `api.atlassian.com`, so the token must be one without scopes.
  - **Server and Data Center:** a personal access token (JIRA 8.14 or later), kept in the credential store. When
    JIRA wants a CAPTCHA solved after failed logins, TAMMi asks the user to log in to JIRA in a browser.
- **Task keys** are JIRA's issue keys, e.g. `ABC-123`. A moved issue is read with its new key.
- **Finding tasks:** with no query, TAMMi lists the user's unfinished issues
  (`assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC`). A query is JQL. An issue key, or a
  URL on the site that shows an issue (its page, or a board or search with `selectedIssue`), finds that issue.
- **Descriptions** are in JIRA wiki markup, as REST API version 2 returns them on every deployment. They aren't
  converted to markdown.
- **Rate limits** are handled as for GitHub.

## Technical Constraints

- **Framework:** .NET MAUI Blazor Hybrid. The UI is written once as Blazor components in a shared
  Razor class library, and each platform has its own host:
  - **Windows:** a .NET MAUI app hosting the UI in a `BlazorWebView`
  - **Linux:** a Photino.Blazor app hosting the same UI, because .NET MAUI doesn't officially support Linux desktop
- **Platforms:** Windows and Linux at minimum, with other platforms possible later
- Platform-specific services (PTY, file system watching, launching processes, the folder picker and app
  data paths) are reached through interfaces in shared code, with an implementation for each host.
- **App data** lives in `%LOCALAPPDATA%\TAMMi\{Config,Data,Cache}` on Windows, and in the XDG base
  directories on Linux (`~/.config/tammi`, `~/.local/share/tammi`, `~/.cache/tammi` by default).
- **Window state:** TAMMi opens its window at the size it had when it was last closed, maximised if it was then.
  The state is kept in `window-state.json` in the Data folder, apart from the settings. If it's missing or can't be
  read, the window opens at its default size.
- **Credentials** for integrations live in the platform secret store, never in settings or data files:
  - **Windows:** Windows Credential Manager, as generic credentials named `TAMMi:<key>`, saved for the local
    machine only.
  - **Linux:** the Secret Service (e.g. GNOME Keyring or KWallet), through libsecret. libsecret is a system
    library and isn't shipped with TAMMi. When libsecret is missing or no Secret Service is running, TAMMi
    shows an error saying how to install one, and integrations can't be authenticated.
  - Each integration has one secret. Values that aren't secret (e.g. a JIRA site URL or user name) are settings.
- Web-based UI components (e.g. xterm.js for the terminal) can be used inside the Blazor UI.
- **Git access is hybrid:**
  - **LibGit2Sharp** for fast in-process reads: status, the git graph and history.
  - **The `git` CLI** for writes (fetch, pull, push, stage, commit) and for worktree and submodule
    operations. This keeps full compatibility with the user's git config, credential helpers and hooks.
  - A `git` install is therefore required.
- The embedded terminal (used for both the Terminal and Agents) needs a PTY behind the platform service
  interfaces, with xterm.js as the renderer:
  - **Windows:** ConPTY (Windows 10 1809 or later).
  - **Linux:** a `/dev/ptmx` pseudoterminal. The process is started with `posix_spawn` rather than `forkpty`,
    because a .NET process can't safely run code in a forked child before `exec`. `posix_spawn` starts the new
    session and opens the terminal without any code in the child. This needs glibc 2.29 or later.
  - xterm.js is vendored in the UI library (`wwwroot/lib/xterm`), so the app needs no network access or npm build.

## Open Questions

None at the moment. Add new questions here. When one is resolved, record the decision in the sections
above and remove the question.

