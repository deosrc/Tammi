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
| **Agent Definition** | A configured agent CLI (e.g. Claude Code) that TAMMi can start: name, command, arguments, and how to pass the initial prompt. |
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
or clone the repository again.

**Metadata.** Each workspace describes itself in its `.tammi/` folder. The app's data folder keeps only
an index of known workspace paths, which Home uses to list workspaces.

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

**Deleting a workspace.**
1. If any worktree has uncommitted changes or unpushed commits, TAMMi lists them and asks the user to confirm.
2. TAMMi removes each Workspace Worktree (`git worktree remove`) and then the Workspace Folder.
3. TAMMi offers to delete the workspace branches from the Local Repositories. Branches that are merged
   or pushed are selected for deletion by default. Others are kept unless the user selects them.

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
- **Terminal**: an embedded terminal, opened in the workspace folder

### Settings

- Default Workspace Parent Folder
- Monitored Folder(s)
- Integrations (credentials, configuration)
- Manually added local repositories
- Tools: the external tools offered under "Open in…"
- Status refresh interval for workspaces that aren't open
- Agents: the Agent Definitions available to start, and the initial prompt template

## Agents

- Agents run as **CLI processes inside the embedded terminal component**. The user interacts with them
  through the terminal, just as they would outside TAMMi.
- Agents are **pluggable**. Each Agent Definition has a name, a command and arguments, and is configured
  much like an "Open in…" tool.
- **Working directory:** an agent starts in the Workspace Folder by default, so it can see and change
  every repository. The user can start it in a single Workspace Worktree instead.
- **Task context:** TAMMi writes the workspace's task details (title, description, links) to a markdown
  context file in `.tammi/` and keeps it up to date. When an agent starts, TAMMi sends it an
  **initial prompt** telling it about the context file. The initial prompt is configurable, with a
  placeholder for the context file's path. Each Agent Definition says how the prompt is passed to its CLI.
- **Claude Code** is the first built-in Agent Definition. Others (e.g. Codex CLI, Gemini CLI) can be
  added as definitions.
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
  | Exited | The process ended normally | No |
  | Error | The process crashed or exited with a non-zero code | No |

  Agents with hooks use Working / Needs input / Idle while running. Agents without hooks show Running.

## Technical Constraints

- **Framework:** .NET MAUI Blazor Hybrid. The UI is written once as Blazor components in a shared
  Razor class library, and each platform has its own host:
  - **Windows:** a .NET MAUI app hosting the UI in a `BlazorWebView`
  - **Linux:** a Photino.Blazor app hosting the same UI, because .NET MAUI doesn't officially support Linux desktop
- **Platforms:** Windows and Linux at minimum, with other platforms possible later
- Platform-specific services (PTY, file system watching, launching processes) are reached through
  interfaces in shared code, with an implementation for each host.
- Web-based UI components (e.g. xterm.js for the terminal) can be used inside the Blazor UI.
- The embedded terminal (used for both the Terminal and Agents) needs a PTY (ConPTY on Windows,
  `forkpty` on Linux) behind the platform service interfaces, with xterm.js as the renderer.

## Open Questions

Record the decision here when an item is resolved, and move it into the sections above.

### Terminal
- Is there one terminal per workspace, or can the user open several (e.g. one per repository)?
- Which shell is used: the platform default (PowerShell on Windows, `$SHELL` on Linux), or a setting?
- Do terminal sessions persist when a workspace tab is closed and reopened, or across app restarts?

### Open in… / Tools
- Which tools are built in (VS Code, Visual Studio, Rider, terminal, file manager)? Does TAMMi detect installed tools automatically?
- What can be configured for each tool: name, executable path, arguments (e.g. a `{path}` placeholder), icon, enabled/disabled?
- Can a tool open the whole workspace folder, a single repository, or both?

### Git
- Which operations are in scope at first beyond status and graph (fetch, pull, push, commit, branch, checkout)?
- Shell out to the `git` CLI or use a library such as LibGit2Sharp?
