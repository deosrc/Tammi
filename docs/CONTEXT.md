# TAMMi: Product Context

> Living document. This is the source of truth for what TAMMi is and the words we use for it.
> Update it as decisions are made. Unresolved items live under [Open Questions](#open-questions).

## Purpose

TAMMi is a **software development workspace management tool** with a **task-first** approach.

The developer starts from the work to be done (one or more tasks), not from a repository. TAMMi
then builds a workspace around those tasks: a named folder holding the relevant git repositories,
with a view of their git state, AI agents working on the tasks, and shortcuts into other tools.

## Glossary

| Term | Meaning |
|------|---------|
| **Task** | A unit of work. It comes from an integration (e.g. a GitHub issue or JIRA ticket) or is defined manually. |
| **Task Source** | Where a task comes from: an integration or manual entry. |
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
| **Open in…** | Actions that open the workspace or a repository in an external tool (IDE, terminal, file manager, …). |

## Core Flow: Creating a Workspace

1. **Choose tasks.** The user picks tasks first, because they drive the rest of the setup.
   - Tasks come from integrations (GitHub, JIRA) or are entered manually.
   - The task list can be changed after the workspace is created.
2. **Name and locate the workspace.**
   - The name defaults to the task name when exactly one task is selected.
   - The parent folder defaults to the **Default Workspace Parent Folder** app setting.
3. **Select repositories** from the Repository Catalogue.
   - **Local Repository:** TAMMi creates a Workspace Worktree for it in the Workspace Folder.
   - **Remote-only Repository:** TAMMi first guides the user through cloning it to a local folder.
     This turns it into a Local Repository, and TAMMi then creates the worktree as normal. The clone
     lives outside the workspace, so other workspaces can reuse it.

### Repositories on disk

```
<clone folder>/<repo>/              Local Repository (shared, outside any workspace)
<parent folder>/<workspace name>/
    <repo>/                         Workspace Worktree of <clone folder>/<repo>
    <another repo>/                 Workspace Worktree of another Local Repository
```

Workspaces never hold full clones. Each repository in a workspace is a worktree of a Local Repository,
so several workspaces can work on the same repository at once without cloning it again.

## Main Screens

### Home

- Lists existing workspaces with a status summary for each, so the user can see what needs
  attention without opening the workspace:
  - **Working directory changes** across the workspace's repositories (e.g. uncommitted changes)
  - **Agent status** for the workspace's AI agents (e.g. running, waiting for input, finished)
- Opening a workspace shows it in a **tab**. Several workspaces can be open at once.
- Starts the Create Workspace flow.

### Workspace View (tab)

- **Side panel** (in this order, top to bottom)
  - Running agents
  - Git status of each repository in the workspace
  - "Open in…" tools
- **Git operations**, including a git graph view
- **Agents**: start, interact with and stop AI agents
- **Terminal**: an embedded terminal, opened in the workspace folder

### Settings

- Default Workspace Parent Folder
- Monitored Folder(s)
- Integrations (credentials, configuration)
- Manually added local repositories
- Tools: the external tools offered under "Open in…"

## Technical Constraints

- **Framework:** .NET MAUI
- **Platforms:** Windows and Linux at minimum, with other platforms possible later
- See [Open Questions](#open-questions) about Linux support

## Open Questions

Record the decision here when an item is resolved, and move it into the sections above.

### Platform
- **MAUI on Linux.** .NET MAUI does not officially support Linux desktop. Options:
  - a community Linux/GTK backend for MAUI
  - MAUI Blazor Hybrid, with a Linux host such as Photino sharing the Blazor UI
  - another cross-platform .NET UI framework (e.g. Avalonia)

  This needs a decision before any UI scaffolding.

### Workspace on disk
- Is a branch created automatically for each task? If so, what naming convention (e.g. `<task-key>-<slug>`)? A worktree needs a branch that isn't checked out in another worktree, so every Workspace Worktree needs its own branch (or a detached HEAD).
- What base does a new worktree branch start from: the default branch of the remote (after a fetch), or the Local Repository's current HEAD?
- What is a worktree's folder name inside the workspace: the repository name? How are name clashes handled (e.g. two repositories called `api` from different owners)?
- Where is workspace metadata stored: inside the workspace folder (e.g. `.tammi/`), in central app data, or both?
- What happens to the folder and branches when a workspace is deleted or archived? Presumably `git worktree remove` for each repository. What if a worktree has uncommitted or unpushed changes?

### Tasks
- Can one workspace mix tasks from different sources?
- Is task state synced back to the integration (status changes, linking PRs)?
- What fields does a manual task have?

### Repositories
- In the clone step for a Remote-only Repository, where does the clone go by default? A Monitored Folder (so it is discovered automatically) seems a natural fit. Can the user choose any folder?
- How is a Local Repository matched to an integration repository, so that it doesn't appear twice in the catalogue? By remote URL? What about forks and multiple remotes?
- What happens if a Local Repository is moved or deleted while workspaces still have worktrees from it?

### Agents
- Which agents are supported first (e.g. Claude Code, others)? Are they run as CLI processes in an embedded terminal or through an SDK/API?
- Does each agent run against one repository or the whole workspace folder?
- How is a task's context passed to an agent?
- What agent states exist (e.g. running, waiting for input, idle, finished, errored)? These states appear on both Home and the Workspace View.

### Home status summary
- How much detail does Home show? Options: per-repository breakdown, or a workspace-level summary such as "3 repos, 2 with changes".
- Which git states count as "changes"? Unstaged, staged and untracked files only, or also unpushed commits and being behind the remote?
- How is the status kept current: file system watchers, polling, or refreshing when Home is shown? This matters most for workspaces that aren't open in a tab.

### Terminal
- Is there one terminal per workspace, or can the user open several (e.g. one per repository)?
- Which shell is used: the platform default (PowerShell on Windows, `$SHELL` on Linux), or a setting?
- Do terminal sessions persist when a workspace tab is closed and reopened, or across app restarts?
- Do CLI-based agents share the same terminal component? Embedding a terminal on Windows and Linux needs a PTY (ConPTY / `forkpty`) and a terminal renderer such as xterm.js. This links to the Platform decision.

### Open in… / Tools
- Which tools are built in (VS Code, Visual Studio, Rider, terminal, file manager)? Does TAMMi detect installed tools automatically?
- What can be configured for each tool: name, executable path, arguments (e.g. a `{path}` placeholder), icon, enabled/disabled?
- Can a tool open the whole workspace folder, a single repository, or both?

### Git
- Which operations are in scope at first beyond status and graph (fetch, pull, push, commit, branch, checkout)?
- Shell out to the `git` CLI or use a library such as LibGit2Sharp?
