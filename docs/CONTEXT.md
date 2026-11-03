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

## Main Screens

### Home

- Lists existing workspaces with a status summary for each, so the user can see what needs
  attention without opening the workspace:
  - **Working directory changes** across the workspace's repositories (e.g. uncommitted changes)
  - **Agent status** for the workspace's AI agents (e.g. running, waiting for input, finished)
- Opening a workspace shows it in a **tab**. Several workspaces can be open at once.
- Starts the Create Workspace flow.

### Workspace View (tab)

- **Side panel**
  - Git status of each repository in the workspace
  - Running agents
  - "Open in…" tools
- **Git operations**, including a git graph view
- **Agents**: start, interact with and stop AI agents

### Settings

- Default Workspace Parent Folder
- Monitored Folder(s)
- Integrations (credentials, configuration)
- Manually added local repositories

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
- How does a repository get into a workspace? Options: a full clone, a `git worktree` from an existing local clone, or a reference/symlink to an existing clone.
- Is a branch created automatically for each task? If so, what naming convention (e.g. `<task-key>-<slug>`)?
- Where is workspace metadata stored: inside the workspace folder (e.g. `.tammi/`), in central app data, or both?
- What happens to the folder and branches when a workspace is deleted or archived?

### Tasks
- Can one workspace mix tasks from different sources?
- Is task state synced back to the integration (status changes, linking PRs)?
- What fields does a manual task have?

### Repositories
- Can a repository from an integration be selected before it has been cloned locally? If so, TAMMi clones it on demand. Where to?
- How are duplicates handled when the same repository appears from several sources?

### Agents
- Which agents are supported first (e.g. Claude Code, others)? Are they run as CLI processes in an embedded terminal or through an SDK/API?
- Does each agent run against one repository or the whole workspace folder?
- How is a task's context passed to an agent?

- What agent states exist (e.g. running, waiting for input, idle, finished, errored)? These states appear on both Home and the Workspace View.

### Home status summary
- How much detail does Home show? Options: per-repository breakdown, or a workspace-level summary such as "3 repos, 2 with changes".
- Which git states count as "changes"? Unstaged, staged and untracked files only, or also unpushed commits and being behind the remote?
- How is the status kept current: file system watchers, polling, or refreshing when Home is shown? This matters most for workspaces that aren't open in a tab.

### Open in…
- Which targets are built in (VS Code, Visual Studio, Rider, terminal, file manager)? Can users configure custom commands?

### Git
- Which operations are in scope at first beyond status and graph (fetch, pull, push, commit, branch, checkout)?
- Shell out to the `git` CLI or use a library such as LibGit2Sharp?
