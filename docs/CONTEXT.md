# Tammi: Product Context

> Living document. This is the source of truth for what Tammi is and the words we use for it.
> Update it as decisions are made. Unresolved items live under [Open Questions](#open-questions).

## Purpose

Tammi is a **software development workspace management tool** with a **task-first** approach.

The developer starts from the work to be done (one or more tasks), not from a repository. Tammi
then builds a workspace around those tasks: a named folder holding the relevant git repositories,
with a view of their git state, AI agents working on the tasks, and shortcuts into other tools.

## Principles

- **Tammi never requires changes to repositories.** It works without adding files, config or commits
  to any repository. Its own files live in the workspace's `.tammi/` folder and in app data.
- **Tammi only reads from integrations**, except that it adds SSH keys to an account when the user asks and the
  token allows it.

## Glossary

| Term | Meaning |
|------|---------|
| **Task** | A unit of work from an integration (e.g. a GitHub issue or JIRA ticket). Tammi doesn't support manually defined tasks: a workspace without tasks is described by its name. |
| **Task Source** | The integration a task comes from. |
| **Task Status** | A task's status as its source names it (e.g. `Open` or `In Review`), with a **status category**: to do, in progress or done. JIRA puts every status, including a site's own, in a category; sources with fixed statuses map theirs to the nearest one. |
| **Workspace** | A named collection of tasks and repositories in a folder on disk. It is the main unit of work in Tammi. Its name is free text, separate from its folder's name, and can be changed after it's created. |
| **Workspace Folder** | The workspace's location on disk: `<parent folder>/<folder name>`. |
| **Repository** | A git repository that can be added to a workspace. |
| **Repository Catalogue** | The app-level list of known repositories, collected from all repository sources. |
| **Repository Source** | Where catalogue entries come from: an integration (e.g. GitHub), a local repository added manually, or a **Monitored Folder**. |
| **Monitored Folder** | A local folder (where repositories are usually cloned) that Tammi scans for git repositories. |
| **Local Repository** | A catalogue repository with a clone on disk, outside any workspace. Workspaces create their worktrees from it. |
| **Remote-only Repository** | A catalogue repository known only through an integration, with no clone on disk yet. It must be cloned, which makes it a Local Repository, before it can be added to a workspace. |
| **Workspace Worktree** | A `git worktree` of a Local Repository, created inside the Workspace Folder. This is how a repository is included in a workspace. |
| **Git Graph** | A repository's (or submodule's) commits drawn as lanes, with their branches and tags, shown as a main area tab in the Workspace View. |
| **Integration** | A connection to an external service (GitHub, JIRA, …). It can provide tasks, repositories or both. |
| **Agent** | An AI coding agent session that runs in the context of a workspace. It can be started, interacted with and stopped. |
| **Agent Definition** | A configured agent CLI (e.g. Claude Code) that Tammi can start: name, command, arguments, how to pass the initial prompt, and how to resume a session. |
| **Agent Session** | One conversation with an agent. Tammi records it so it can be resumed after a restart, with the agent's own resume command. |
| **Supported Agent** | An agent CLI that Tammi knows how to start, with a built-in Agent Definition (e.g. Claude Code). It's always offered and isn't configured by the user. |
| **Custom Agent** | An agent CLI the user sets up with their own Agent Definition (e.g. Codex CLI or Gemini CLI). |
| **Tools** | Actions that open the workspace or a repository in a tool (IDE, terminal, file manager, …). |

## Core Flow: Creating a Workspace

1. **Choose tasks.** The user picks tasks first, because they drive the rest of the setup.
   - Tasks come from integrations (GitHub, JIRA).
   - Tasks are optional. A workspace with no tasks is described by its name alone.
   - One workspace can mix tasks from any sources.
   - Tammi only reads tasks from integrations. It doesn't write back status changes or PR links yet.
   - The task list can be changed after the workspace is created.
   - **Finding tasks.** One search box covers every connected Task Source at once, and the results are combined,
     most recently updated first. What the user enters decides what's searched:
     - **Nothing:** the tasks assigned to the user that are still to do: open GitHub issues, and JIRA issues whose
       status is in the To Do category (not in progress or done). Tasks already in a workspace are left out.
     - **A link or reference** to a task, e.g. an issue's URL, `owner/repo#42` or `ABC-123`: that task, from each
       source that recognises it. This is the only way to find public tasks outside the user's organisations. A link
       that no source recognises isn't searched.
     - **Any other text:** a search of the text of the tasks of the user and their organisations that aren't done:
       open GitHub issues, and JIRA issues whose status isn't in the Done category (see [Integrations](#integrations)).
       Tasks already in a workspace are found too, and marked with its name.
   - Search starts when the user stops typing. A source that fails doesn't stop the others: its error is shown
     with their results.
   - Tasks chosen from several searches and sources are kept together in one list.
2. **Name and locate the workspace.** The workspace has a **name** and a **folder name**, which are set separately.
   - **Name:** shown on Home and the workspace's tab. It's free text: anything but blank, without line breaks or other
     control characters. Surrounding spaces are removed. It can be changed later (see
     [Workspace View](#workspace-view-tab)).
   - When exactly one task is selected, the name defaults to the **Suggested workspace name** template from the
     General settings, filled in from the task. Its placeholders are `{TaskReference}` (e.g. `api#42` or `ABC-123`),
     `{TaskKey}` (e.g. `acme/api#42`), `{TaskTitle}`, `{RepositoryOwner}` and `{RepositoryName}` (e.g. `acme` and
     `api`, only for tasks that belong to a repository, such as GitHub issues). They're matched ignoring case, other
     text in braces is left as it is, and runs of spaces and line breaks become one space. The default is
     `{TaskReference} {TaskTitle}`, so `JIRA-123` "Add some feature" suggests `JIRA-123 Add some feature`.
   - When the template can't be filled in (no tasks or several, a placeholder the task has no value for, or a blank
     template), the name follows the folder name instead.
   - **Folder name:** the Workspace Folder's name. It must be a folder name that's valid on every platform: no
     `/ \ : * ? " < > |` or control characters, not `.` or `..`, no trailing full stop, not a name Windows reserves
     (e.g. `CON` or `NUL`), and at most 255 bytes. Surrounding spaces are removed.
   - When exactly one task is selected, the folder name defaults to one made from the task, in the **Suggested folder
     name** format from the General settings. Otherwise it's made from the name the user entered, in the same format,
     or the user enters it.
   - Until the user edits them, the name and folder name follow what they're made from; clearing one makes it follow
     again the next time that changes. Only one follows the other at a time: the folder name follows the name only
     when the user entered the name, and the name follows the folder name only when it can't be suggested from the
     task.
   - The suggested folder name is the task's title, after its reference unless that's turned off. A GitHub issue's
     reference is its repository and number (`acme/api#42` gives `api#42`), and a JIRA issue's is its key. The format
     sets:
     - **Separator:** hyphens (the default) or underscores between words, leaving out punctuation (apostrophes are
       dropped, so "don't" stays one word), or spaces, keeping punctuation except characters a folder name can't have.
     - **Case:** lowercase (the default), uppercase, or as written.
     - **Task reference:** whether the name starts with it (the default).

     So by default, `JIRA-123` "Add some feature" suggests `jira-123-add-some-feature`. A folder name longer than 80
     characters is cut at a word, keeping the reference.
   - The parent folder defaults to the **Default Workspace Parent Folder** app setting. It must be a full path. It
     doesn't need to exist yet: it's created with the workspace.
   - The Workspace Folder must not already exist, as a folder or a file.
3. **Select repositories** from the Repository Catalogue.
   - **Local Repository:** Tammi creates a Workspace Worktree for it in the Workspace Folder.
   - **Remote-only Repository:** Tammi first guides the user through cloning it to a local folder.
     This turns it into a Local Repository, and Tammi then creates the worktree as normal. The clone
     lives outside the workspace, so other workspaces can reuse it.
   - The clone defaults to `<Monitored Folder>/<repo>`, so Tammi finds it automatically. If there are
     several Monitored Folders, the user picks one. The user can choose any other folder instead, and
     the clone is then added as a manually added local repository.
   - **Task repositories are chosen automatically.** A task that belongs to a repository (e.g. a GitHub issue) chooses
     the Local Repository with a remote matching that repository's URL. URLs are compared by host and path, ignoring
     case, the scheme, the user, the port and a trailing `.git`, so HTTPS and SSH remotes both match. When several
     clones match (e.g. a clone of a fork with an `upstream` remote, and a clone of the repository itself), the one
     whose fetched remote matches is chosen.
     - It happens when the repository step opens, once for each task: a repository the user removes isn't chosen
       again. A repository that's already chosen isn't chosen twice.
     - Removing a task removes the repositories that were chosen only for it.
     - A task whose repository has no clone yet chooses nothing. (Offering to clone it is part of Remote-only
       Repositories.)
   - Choosing a repository fetches it, to find the remote's default branch (see [Branches](#repositories-on-disk)).
     A repository that can't be fetched still offers the branches fetched before, and one with no remotes needs its
     base branch chosen.
   - A workspace can be created without repositories.
4. **Create the workspace.** Tammi checks everything first (the Workspace Folder is still free, the branch name is
   valid and doesn't exist in any of the repositories, each base branch exists, the folder names are valid and
   different), so nothing is created when a check fails. It then creates the Workspace Folder (and its parent
   folders), fetches each repository again and creates its worktree and submodules, and writes the metadata and index
   entry last, showing each step as it goes.
   - A repository that can't be fetched now is still created, from the last fetched base, with a warning.
   - If a step fails, or the user cancels (or leaves the page), Tammi removes everything it created: the worktrees,
     their new branches, the Workspace Folder and any parent folders it created that are still empty. Anything it
     couldn't remove is listed. Since the metadata is written last, a workspace that's only partly created is never
     listed on Home.
   - The new workspace then opens in a tab, unless there were warnings, which are shown first.

### Repositories on disk

```
<clone folder>/<repo>/              Local Repository (shared, outside any workspace)
<parent folder>/<folder name>/
    .tammi/                         Workspace metadata (tasks, repositories, branches, settings)
                                    and the task context file for agents
    <repo>/                         Workspace Worktree of <clone folder>/<repo>
    <another repo>/                 Workspace Worktree of another Local Repository
```

Workspaces never hold full clones. Each repository in a workspace is a worktree of a Local Repository,
so several workspaces can work on the same repository at once without cloning it again.

**Finding Local Repositories.** The catalogue's Local Repositories come from the Monitored Folders and the manually
added local repositories in the Repositories settings. A repository found by both, or in two overlapping Monitored Folders, is
listed once.
- A folder is a repository when it has a `.git` folder, or a `.git` file that doesn't belong to a linked worktree.
- Scanning a Monitored Folder looks up to 20 folders down. It doesn't look inside repositories (so submodules and
  repositories nested in another aren't listed on their own), and it skips linked worktrees, Workspace Folders, hidden
  folders and links to other folders.
- A manually added repository must be a repository's top folder, and not a worktree. It can't be in a Monitored
  Folder, because the scan already finds it.
- Adding a Monitored Folder that holds manually added repositories asks the user to confirm, then removes those
  repositories from the manually added ones.
- Missing folders and repositories that can't be read are left out of the catalogue and shown on the Settings screen.
- The catalogue is scanned when it's first needed, when the user asks (Rescan on the Settings screen), when the
  Monitored Folders or manually added repositories change, and when folders appear in or disappear from a Monitored
  Folder. Only a Monitored Folder's own entries are watched, not its subfolders, so watching a large clone folder
  doesn't use up the platform's watch limits. A repository cloned deeper down is found by the next scan.

 A local clone and an integration repository are the
same catalogue entry when any of the clone's remotes matches the integration repository's URL. URLs are
normalised before comparing, so HTTPS and SSH forms match. A clone of a fork with an `upstream` remote
therefore counts as a Local Repository for both the fork and the upstream repository.

**Moved or deleted Local Repositories.** Tammi detects when a Workspace Worktree's Local Repository
has been moved or deleted, and marks the workspace as broken on Home and in the Workspace View. It
offers to repair the worktree: either point it at the repository's new location (`git worktree repair`)
or clone the repository again. `git worktree repair` doesn't fix the worktree's submodules, whose links to their
git data are relative paths, so Tammi relinks them itself. Moving a Workspace Folder breaks its worktrees in the
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
- If the index can't be read, no workspaces are listed and Tammi keeps a copy of it as `workspaces.invalid.json`.
- A repository's folder name must be a single folder name, never a path, so nothing Tammi does to a worktree can reach
  outside the Workspace Folder.

**Folder names.** A Workspace Worktree's folder defaults to the repository name (e.g. `api`). If two
repositories in the workspace have the same name, Tammi prefixes the owner (e.g. `acme-api`). The user
can change any folder name during setup.
- The owner comes from the URL of the remote that's fetched (`origin`, or the first remote if there's no `origin`),
  e.g. `acme` for `git@github.com:acme/api.git`. A repository with no remotes uses the name of the folder it's in.
- Names are compared ignoring case, as on Windows. A name the user chose counts as taken, and so does `.tammi`. If the
  owner doesn't tell two repositories apart, a number is added (e.g. `api-2`).
- Folder names follow the repositories chosen until the user edits them, like the workspace's folder name.

**Branches.** Git won't check out one branch in two worktrees, so each Workspace Worktree gets its
own branch. It is created automatically when the workspace is created:
- Tammi suggests a branch name from the task, e.g. `<task-key>-<slug>`. When there are no tasks or
  several tasks, it uses the workspace name instead.
  - The task key is its reference as in the suggested folder name, as written, so a JIRA key stays uppercase and
    JIRA links the branch to the issue. The slug is the title (or the workspace name) in lowercase words joined by
    hyphens, without punctuation. So `ABC-123` "Add some feature" suggests `ABC-123-add-some-feature`, and
    `acme/api#42` "Fix login" suggests `api-42-fix-login`. It's cut at a word to at most 80 characters.
  - It follows the tasks and the workspace name until the user edits it.
- The user can edit the suggested name before the workspace is created.
- The same branch name is used in every repository in the workspace.
- **Base:** Tammi fetches each Local Repository, then branches from the remote's default branch
  (e.g. `origin/main`). The user can choose a different base branch for each repository, from its remote-tracking
  and local branches or by entering any commit-ish.
- The new branch doesn't track its base, so pushing it publishes it under its own name rather than to the base.

**Submodules.** When a Workspace Worktree is created, Tammi checks out its submodules automatically
(`git submodule update --init --recursive`).

**Renaming a workspace.** The Workspace View's **Manage Workspace** menu offers **Rename…**, which changes the
workspace's name (as checked when it's created) in its metadata and index entry, and on its tab. The Workspace Folder
keeps its name, so worktrees and running agents and terminals aren't affected. The task context file is written again
with the new name, without reading the tasks again. A workspace that can't be read can't be renamed.

**Deleting a workspace.** The Workspace View and the details tab of a workspace that can't be read both offer it.
1. Tammi checks what would be lost and always asks the user to confirm. It lists the worktrees with uncommitted changes
   or unpushed commits (including inside their submodules), and anything else in the Workspace Folder that isn't a
   Workspace Worktree or `.tammi/` (e.g. files an agent wrote, or a full clone), since it's deleted with the folder. A
   worktree whose Local Repository is missing can't be checked, so it's listed too.
2. Tammi removes each Workspace Worktree (`git worktree remove`), then the Workspace Folder, then the workspace's index
   entry. If a worktree has new changes, or something new appears in the folder, since the user confirmed, it stops
   and the user checks again. Links in the folder are deleted without what they point at. `.tammi/` is deleted last,
   so a folder that's only partly deleted is still the workspace and can be deleted again. A worktree whose folder
   is already gone is only unregistered, and one git no longer lists (e.g. after `git worktree prune`) is skipped.
3. Tammi offers to delete the workspace branches from the Local Repositories. Branches that are merged or pushed are
   selected for deletion by default. Others are kept unless the user selects them. Other branches are never offered.
   - **Merged** means the branch's tip is reachable from its base branch (e.g. `origin/main`). Squash and rebase
     merges aren't detected.
   - **Pushed** means every commit on the branch is on a remote-tracking branch, as for unpushed commits on Home.
- **A workspace whose metadata can't be read** is deleted the same way. Worktrees are always created directly in the
  Workspace Folder, so Tammi finds them by looking at its folders. Their checked-out branches are offered, but not
  selected, because without the metadata Tammi can't tell they're the workspace's own.
- **A workspace whose folder is missing, or now holds a different workspace,** only has its index entry removed. The
  folder isn't this workspace's to delete.

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
  - Agent status is always live, because Tammi runs the agent processes itself.
- Workspaces are listed in the order they were first saved or added, each with its name and Workspace Folder.
  A workspace whose folder or metadata is missing, or whose metadata can't be read, can't be opened. Its card says
  why in a few words ("Workspace folder missing" or "Corrupt"), and its Details button opens a tab with the full
  reason and the option to delete the workspace.
- Opening a workspace shows it in a **tab**. Several workspaces can be open at once. Opening a workspace that's
  already open shows its tab.
- Starts the Create Workspace flow.

### Workspace View (tab)

- **Side panel** (in this order, top to bottom)
  - Tasks: the workspace's tasks
  - Agents
  - Repositories: the git status of each repository in the workspace
  - Tools
- **Tasks section:** a card for each of the workspace's tasks, in the workspace's order, with its source's logo, its
  short reference (as in suggested names, e.g. `api#42` or `ABC-123`, with the full key as its tooltip), its summary
  and its Task Status, as a chip coloured by its category. Clicking the card, once its task has been read, opens the
  task's page in the browser.
  - Tasks are read from their sources when the tab opens, and again on Refresh or when integrations are added or
    removed. Switching between open tabs doesn't read them again. Each task is read on its own, so a failing source
    doesn't hold up the others.
  - A task that can't be read says why: its Task Source isn't connected, it wasn't found (or the user can't see it),
    or the source failed (with the error as a tooltip).
  - **GitHub** issues are Open (to do), or Closed, Not planned or Duplicate (done), from the issue's state and why it
    was closed. GitHub has no in-progress state. **JIRA** issues have their own status and its category.
- **Side panel cards** that do something when clicked (a task's, an agent's and a repository's) do it wherever the card
  is clicked, and show it with a pointer and the card's colour on its border. Each card's name is also a button, so
  the keyboard can reach it.
- **Repositories section:** a card with the git status of each repository, in the workspace's order. Right-clicking a
  card opens a menu with a **Tools** submenu (see [Tools](#tools)). The whole card is coloured by what needs
  attention: danger when the worktree is broken or its status can't be read, warning when it has work that isn't
  pushed (uncommitted changes or unpushed commits) or is behind its upstream, in it or any of its submodules, and
  success otherwise. Each card has:
  - a label: Broken, Unreadable, Changed (as on Home), Out of sync (only behind) or Clean
  - its branch (or the commit, when HEAD is detached), and how far it's ahead of and behind its upstream, when it
    tracks one
  - how many uncommitted changes and unpushed commits it has
  - its submodules, nested under it, each with the same details, or "Not checked out"
  - a broken worktree says what's wrong (e.g. "Local Repository moved or deleted") instead
- **Live updates:** while a workspace's tab is open (even when another tab is shown), Tammi watches each worktree's
  folder and its git data in the Local Repository: the worktree's git directory (index, HEAD and its submodules' git
  directories), the Local Repository's refs and the entries directly in its `.git` folder (e.g. `packed-refs`). So
  editing, staging, committing and fetching all update the status, whichever tool does them. Only the repository that
  changed is read again. Closing the tab stops watching, and so does starting to delete the workspace (it starts
  again if the delete is cancelled).
- **Git operations**, including the Git Graph
- **Git Graph:** a main area tab named `Git: <repository folder>` (e.g. `Git: api`), or `Git: <repository
  folder>/<submodule path>` for a submodule (e.g. `Git: api/libs/core`). It's opened from the **+** menu's **Git**
  item, which lists **Repository: `<name>`** for each repository and **Submodule: `<name>/<path>`** for each
  checked-out submodule (nested ones too), or by clicking a repository's card in the side panel (a checked-out
  submodule's part of the card opens the submodule's graph). Each repository or submodule has at most one graph tab
  at a time: opening it again, either way, shows the open one.
  - It lists commits newest first in topological order (children before parents), each with its lane and dot, its
    branch and tag labels, its subject, author, date and (if shown) short commit id. Merges join lanes, and lanes that
    end are reused, so the graph stays narrow. HEAD's dot is a ring, the branch HEAD is on is highlighted, a
    detached HEAD has its own label, and tags have a tag icon.
  - The columns (Graph, Description, Author, Date and Commit) have headings, which stay in view as the list scrolls.
    Right-clicking the headings opens a menu that shows or hides each column except Graph and Description. The Commit
    column (the short commit id) is hidden until it's shown. Below a divider, the menu shows or hides the rows' tags
    and remote-tracking branches (both shown until they're hidden). The details always show every label. Like the
    widths, these choices are shared by every graph until the app closes.
    Description takes the space left. Each other column can be resized from its edge towards Description: drag the
    handle, or focus it and use Left and Right. Double-clicking a handle resets its column. Until it's resized, the
    Graph column fits the graph's lanes, and a wider graph is cut off at the column's edge. The widths are shared by
    every graph until the app closes.
  - A toggle chooses which commits it shows: **Workspace branch** (HEAD, the workspace branch, its upstream and its
    base branch; HEAD and its branch's upstream for a submodule) or **Everything** (every local and remote-tracking
    branch and tag). It starts as Settings → Git says, which defaults to the workspace branch.
  - Large histories stay fast: only the rows in view are drawn, and commits are read 500 at a time as the list
    scrolls. The repository stays open while the tab is, so later pages don't walk the history again.
  - Selecting a commit (by clicking it, or with Up, Down and Home) shows its details beside the list, top to bottom:
    - a bar with its short id in the middle. Clicking the id copies the full id.
    - its subject, in larger text, written as in the message.
    - its message's body, which scrolls. The split below it resizes it, by dragging or by focusing it and using Up and
      Down, down to three lines. Double-click resets it. Its height is kept like the details' width. A message with
      no body has neither.
    - its authors: the author, the committer when they're someone else, and anyone in the message's
      `Co-authored-by:` trailers. The section collapses to its heading, with the author's name beside it.
    - after a divider, the files it changed compared with its first parent, with renames detected, and how many were
      modified, added, deleted and renamed, each as a coloured icon (a pencil, a plus, a trash can and an arrow) and a
      number, leaving out any with none. A toggle shows them as a list (each file's name in bold, then its folder) or
      as a tree of folders, each with a folder icon and, after its name, the same counts for the files in it, which
      start closed and open and close, with folders holding only one folder joined (e.g. `src/app`). Each file keeps
      its A, M, D or R.
    - Every graph shows files the same way, and the list or tree choice is kept in the settings, so it's the same
      after a restart. Like the columns, whether Authors is collapsed is shared by every graph until the app closes.
      Diffs are a separate Git operation.
  - The split between the list and the details is resizable: drag it, or focus it and use Left and Right. Double-click
    resets it. The details' width is kept as a fixed width in pixels (see [Layout state](#technical-constraints)), so
    every graph opens with it, even after a restart. Each side keeps a minimum width: when the window is too narrow
    for the kept width, the details are narrower for the time being, and go back to it when there's room.
  - While it's shown and the workspace's tab is open, it reads the repository's refs again whenever the repository
    changes, and reads the graph again only if they've changed (e.g. after a commit, fetch or checkout), keeping the
    selection. A graph that isn't shown does the same when it's next shown. Refresh reads it again on demand.
  - Like terminals, graph tabs stay when the workspace's tab closes. Closing a graph's tab closes the repository, and
    deleting the workspace closes its graphs.
- **Agents**: start, interact with and stop AI agents (see [Agents](#agents))
- **Agents section:** a card for each of the workspace's open agents, in the order they started, with its
  [state](#agents) as a badge and the card's colour. Clicking the card shows its tab, and a running agent has a
  **Stop** button (which only stops it). An agent that ended with an error shows its exit code. Below the cards,
  **Past sessions** lists the workspace's recorded Agent Sessions that aren't running, most recently started first,
  each with its agent's name, the repository it was started in (if it wasn't the Workspace Folder) and when it was
  last started. Each one can be resumed, which opens it in a new tab, or forgotten. A session whose agent can't resume
  it says why instead.
- **Main area tabs:** the main area has its own row of tabs, below a small divider. The first is always the
  **dashboard** (an icon, no name), which the view shows until the user opens something else. Then come the
  workspace's terminals, agents and Git Graphs, in the order they started or opened. An agent's tab has a dot coloured by its state. The
  last is always a **+** tab, which opens a menu of things to start: **Terminal**, **Git** (see Git Graph), then each
  Agent Definition (in the order agents are offered). Terminal and each Agent Definition have a submenu of
  **Workspace** and **Repository: `<name>`** for each repository.
- **Terminals**: any number of embedded terminals, shown as tabs alongside agent sessions. A new
  terminal opens in the workspace folder by default, or the user can open one in a specific repository.
  The shell defaults to PowerShell on Windows (`pwsh` if installed) and `$SHELL` on Linux, and can be
  changed in Settings.
  - A terminal is named "Terminal" when it's in the Workspace Folder, or after the repository's folder (e.g. `api`).
    A name already used by one of the workspace's open terminals gets the first free number (e.g. "Terminal 2").
  - Closing a terminal's tab ends its shell, and shows the next tab (or the previous one, or else the dashboard).
    A terminal whose shell has exited stays, marked as exited, until its tab
    is closed.
  - Terminals keep running when the workspace's tab closes (see [Lifetime](#agents)). Each one keeps its latest
    1 MiB of output, so reopening the tab shows it again, and the view shows the tab it was showing.
  - Deleting a workspace closes its terminals and agents first.
- **Agents** open as tabs alongside terminals, and work like them, except:
  - An agent is named after its Agent Definition when it's in the Workspace Folder (e.g. "Claude Code"), and after
    its definition and repository folder when it's in a repository (e.g. "Claude Code (api)"). A name already used by
    one of the workspace's open agents gets the first free number (e.g. "Claude Code 2"). A resumed session is named
    after the folder it was started in.
  - **Stopping** an agent (from the side panel) ends its process, and keeps its tab, marked as exited, until it's
    closed. Closing a running agent's tab stops it. Either way, its session can still be resumed.
  - A session that's already running in one of the workspace's agents can't be resumed again.
  - **Copy and paste:** Ctrl+Shift+C and Ctrl+Shift+V on every platform. On Windows, as in Windows Terminal,
    Ctrl+C also copies when text is selected (otherwise it interrupts the process) and Ctrl+V pastes.

### Settings

Settings are grouped into sections, listed in a vertical tab list down the left of the screen:

- **General**
  - Default Workspace Parent Folder (defaults to `Workspaces` in the user's home folder)
  - Suggested workspace name: the template, with its placeholders listed, a warning for text in braces that isn't
    one, and examples of the names it suggests (see [Name and locate the workspace](#core-flow-creating-a-workspace))
  - Suggested folder name: separator, case and whether to start with the task's reference, with examples of the
    folder names it suggests
  - Log level: Trace, Debug, Information (the default), Warning or Error. It applies straight away. A button opens the
    folder the log files are in (see [Technical Constraints](#technical-constraints)) in the system file manager.
- **Integrations:** the connected integrations, each with its connection status, in alphabetical order
  - A single Add button opens a menu of the integration types (GitHub, JIRA), in alphabetical order.
  - The user adds GitHub (github.com or a GitHub Enterprise Server) and JIRA sites, authenticates them again with a new
    token, tests them and removes them.
  - Tammi checks a token with the service before saving it, so a rejected token is never saved and an integration is
    never added without working credentials.
  - Each integration's status is checked when the section opens and on demand: connected (with the account's name),
    not authenticated (no token saved), authentication failed (the token was rejected), or couldn't connect (with the
    reason, e.g. the service can't be reached or there's no credential store).
  - Removing an integration deletes its token. An integration's ID comes from the service it connects to (`github`
    for github.com, `github-<host>` for an Enterprise Server, `jira-<host>[-<path>]` for a JIRA site), so connecting
    the same service again gives the same ID, and workspace tasks from it find their source again. The same service
    can't be connected twice.
- **Git**
  - The versions of the git CLI and of libgit2 (used through LibGit2Sharp) that Tammi uses
  - **Author:** the name and email address git records on commits (`user.name` and `user.email` in the user's global
    git config, `git config --global`). Tammi uses the git CLI and the user's own config, so these apply to every git
    tool on the computer, and the section says so. Clearing one unsets it.
  - **SSH keys:** the key pairs in the user's SSH folder (`~/.ssh`): each `.pub` file with a private key next to it,
    with its type, comment and SHA256 fingerprint. Like the author, they're shared with every SSH tool.
    - **Generate:** Ed25519 (the default) or RSA 4096 with `ssh-keygen`, given a file name (suggested from the type,
      e.g. `id_ed25519`, and never one that's taken), a comment (the author's email by default) and an optional
      passphrase, entered twice. Tammi finds `ssh-keygen` on the path, or on Windows in Git for Windows. Without it,
      keys can't be generated. The SSH folder is created for the user only if it's missing.
    - **Remove:** deletes both files, after the user confirms. It can't be undone, and Tammi doesn't remove the key
      from services.
    - **Copy public key** to the clipboard.
    - **Add to a service:** for each GitHub integration, the key shows whether it's on the account (added, not added,
      or unknown when the token can't list keys), with an Add button. Tammi adds it through GitHub's API, titled with
      the computer's and key's names. If the token isn't allowed to, Tammi copies the public key and opens GitHub's
      page for adding a key, and says why. JIRA has no SSH keys.
  - What a [Git Graph](#workspace-view-tab) shows when it opens: the workspace branch (the default) or everything
- **Repositories**
  - Monitored Folder(s), each with how many repositories were found in it
  - Manually added local repositories
  - The Repository Catalogue's size, with a Rescan button
  - Status refresh interval for workspaces that aren't open (whole minutes from 1 to 1440, default 5)
- **Agents**
  - **Supported Agents** are always listed, and can't be edited or removed. Each one shows whether its CLI is ready:
    Tammi finds the command and runs it to get its version (e.g. `claude --version`). If that fails, it says why
    (not found, failed with an error, or didn't finish) and links to the agent's install guide. The user can check
    again after installing it.
  - **Custom Agents** are added, edited, reordered and removed by the user. Each one has a name, command, arguments,
    initial prompt arguments, session ID arguments and resume arguments, and shows the command lines it starts with. Each agent needs its own name, ignoring
    case and surrounding spaces, including the Supported Agents' names. A name that's already used is still saved,
    but shown as an error until it's changed.
  - Agents are offered in that order: the Supported Agents, then the Custom Agents.
  - The initial prompt template. A menu fills it in with one of three predefined prompts, ticking the one in use. Each
    tells the agent to read the task context file, and to read it again for the tasks' latest details, since Tammi
    rewrites it when they change. Then it has the agent:
    - **Implement the tasks:** start making the changes.
    - **Plan the change:** plan the changes, and wait for the user to approve the plan before making them.
    - **Read the tasks only** (the default): stop, and wait for the user's instructions.

    Clearing the template restores the default.
- **Tools**
  - What tools open for a workspace with only one repository: the repository (the default) or the Workspace Folder
    (see [Tools](#tools)).
  - The tools (see [Tools](#tools)). First Terminal, with the terminal shell's settings (below). Then the other
    predefined tools, each with whether its executable was found (and where), what it opens, its scope (folder tools)
    and its command. Then the custom tools, added as a folder or file tool (which can't be changed afterwards), then
    edited, reordered and removed like Custom Agents. Each shows its kind, whether its executable was found, the
    placeholders it can use, warnings for placeholders it can't use or that don't exist, and the command it starts. A
    **Look again** button finds the executables and icons again, e.g. after installing a tool.
  - A custom tool's settings are hidden until its card is expanded (with the arrow in its heading), except for a tool
    that's just been added, so the list stays short.
  - Each tool but Terminal has a switch beside its name that enables it. A disabled tool's card is collapsed to its
    icon, switch and name.
  - Terminal shell: executable and arguments. Leaving the executable empty uses the platform shell, with its own
    arguments. Changes apply to terminals started afterwards.

Lists of arguments (for the terminal shell and Custom Agents) are edited one argument per line, so they need no
quoting. Blank lines are ignored.
- **About:** the Tammi version, and the git CLI and libgit2 versions

Changes are saved as soon as they're made. The settings live in `settings.json` in the app's Config folder (see
[Technical Constraints](#technical-constraints)):
- The file has a `version`. When its layout changes, the version goes up and Tammi migrates older files as it
  loads them. A file from a newer Tammi is read as far as possible.
- Missing settings take their default values, and out-of-range values are brought into range.
- If the file can't be read, Tammi uses the defaults, says so on the Settings screen, and keeps a copy of the file
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

## Tools

- A **tool** opens the Workspace Folder, a repository or a file in another app. There are two kinds:
  - **Folder tools** open a folder, e.g. VS Code or the file manager. Each has one entry for each folder its scope
    includes.
  - **File tools** open files found in the repositories by their **file patterns**, e.g. Visual Studio for `*.sln`,
    `*.slnf` and `*.slnx`. Each has an entry for each file found, and none where there aren't any. For the Workspace
    Folder, every repository is searched. Patterns are looked for in a repository and its subfolders, ten folders
    deep, skipping hidden folders (e.g. `.git`), `bin`, `obj`, `node_modules`, `packages` and links. A file name
    pattern (e.g. `*.sln`) matches files in each of them. A path pattern (e.g. `.devcontainer/devcontainer.json`) is a
    path from each of them, and can name hidden folders. A leading `/` keeps a pattern to the repository's own folder
    (e.g. `/global.json`). `*` and `?` are wildcards, and case is ignored. A file that several patterns match is found
    once, matched by the first of them.
- Each tool has: **kind**, **name**, **file patterns** (file tools only), **executable**, **arguments**, **scope**
  (folder tools only), **icon** and **enabled/disabled**. The executable is a name on the search path or a full path,
  with environment variables (`%NAME%`) filled in. Arguments are passed as-is, without a shell, so a path with spaces
  needs no quoting.
- The name and arguments can have **placeholders**, filled in for each entry (matched ignoring case):
  `{PreferredFolder}` (the folder the tool should open: see below), `{LaunchFolder}` (the Workspace Folder or
  repository the entry is offered for), `{WorkspaceFolder}`,
  `{RepositoryFolder}` (the repository the entry is offered for, or that the file is in), and, for file tools only,
  `{FileDirectory}`, `{FileName}`, `{FileNameWithoutExtension}`, `{FilePath}` and `{MatchFolder}` (the folder the
  pattern matched from: the file's folder for a file name pattern, or where a path pattern starts, e.g. the folder
  holding `.devcontainer/`). A file tool starts in its `{MatchFolder}`. An entry is left out where a placeholder it
  uses has no value (e.g. `{RepositoryFolder}` for the Workspace Folder). Other text in braces is left as it is, and
  Settings warns about it.
- **Workspaces with one repository:** a setting says what tools open when they're launched at workspace level in a
  workspace with only one repository: **the repository** (the default) or **the Workspace Folder**. It decides
  `{PreferredFolder}`: for the Workspace Folder of such a workspace it's the repository, unless the setting says the
  Workspace Folder; everywhere else it's the same as `{LaunchFolder}`. A folder tool that uses `{PreferredFolder}`
  opens, starts in and shows that folder. Terminal and the predefined folder tools use it, and new custom folder tools
  start with it as their argument.
- **Scope** sets what a folder tool is offered for: the Workspace Folder, the repositories, or both (the default).
  File tools have no scope: they open the files they find, wherever they find them, so they're offered for both.
- The side panel's **Tools section** lists each tool once (a file tool once per file it found), in the order tools
  are offered. Right-clicking a tool opens a menu of each folder it can open: **Workspace**, then
  **Repository: `<name>`** for each repository (for a file tool, the file's repository). Levels that would start the
  same command (e.g. a file tool whose arguments only use the file, or Terminal in a workspace with one repository)
  are one item, the first. Clicking a tool opens its Workspace Folder entry, or its only launch if it has just one;
  only a tool with several repository launches and none for the Workspace Folder shows the menu instead. Right-clicking a repository's card opens a menu with a
  **Tools** submenu of every tool that can open that repository. Menus close on Escape or a click outside them, and
  stay inside the window.
- Launching a tool from the Tools section (by clicking it, or from its menu) pulses its background once, briefly, to
  show the click was seen while the tool starts.
- A tool is only offered when it's enabled and its executable is found (except Terminal, which has none).
- **Icon:** the application's own icon by default, or one of Tammi's generic icons (application, code editor, IDE,
  container, folder, terminal), or a custom image file, chosen with the system's file dialog (PNG, SVG, JPEG, GIF,
  WebP, ICO or BMP, up to 1 MB). When the application's or custom icon can't be found, the tool's generic icon is
  shown. Application icons are found:
  - on Linux, from the application's desktop entry (`.desktop` file) in the XDG data folders: the one whose `TryExec`
    or `Exec` command is the executable, or has its name (ignoring `.sh`). Its `Icon` is a path, or a name looked for in
    the `hicolor` icon theme and `pixmaps`.
  - on Windows, as the icon the shell shows for the executable. A batch file (e.g. VS Code's `bin\code.cmd`) uses the
    icon of the executable with its name in the folder above (`Code.exe`), if there is one.
- **Predefined tools** come with Tammi, built as ordinary tool definitions. They can be enabled or disabled, but not
  changed or removed. They're offered first, then the **custom tools**, in the user's order:
  - **Terminal** is special: it opens one of Tammi's own terminals in a new tab of the Workspace View (in the
    `{PreferredFolder}`), rather than starting an app, so it's the same as **+** → Terminal, which stays. It has
    no executable and is always enabled: Settings has no switch for it. It's always first, in Settings and in the
    tools lists.
  - **VS Code:** `code {PreferredFolder}`.
  - **Dev Container:** a file tool for `.devcontainer/devcontainer.json` and `.devcontainer.json`, running
    `devcontainer open {MatchFolder} --config {FilePath}`. It opens the folder the configuration belongs to (the one
    holding `.devcontainer.json`, or the parent of the `.devcontainer` folder) in the container the file describes, so
    each project of a monorepo with its own configuration has its own entry. It needs the `devcontainer` CLI that VS Code's Dev Containers extension installs (its
    "Install devcontainer CLI" command).
  - **Visual Studio** (Windows only): a file tool for `*.sln`, `*.slnf` and `*.slnx`, named
    `Visual Studio - {FileName}`, started through the Visual Studio Version Selector
    (`%CommonProgramFiles(x86)%\Microsoft Shared\MSEnv\VSLauncher.exe {FilePath}`), which picks the version of Visual
    Studio each solution was made for.
  - **Rider:** `rider {PreferredFolder}`, found on the search path (e.g. JetBrains Toolbox's scripts folder).
  - **File manager:** `explorer {PreferredFolder}` (File Explorer) on Windows, `xdg-open {PreferredFolder}` on Linux.
- Every other tool is an external app, and **keeps running when Tammi exits**. On Linux they're started in their own session (with
  `setsid`), without Tammi's terminal or output. On Windows, a batch file (e.g. a launcher script on the search path)
  runs without a console window. A file tool starts in the file's folder, and a folder tool in its folder.
- The entries are worked out when the workspace's tab opens, and again when the workspace or the settings change.

## Agents

- Agents run as **CLI processes inside the embedded terminal component**. The user interacts with them
  through the terminal, just as they would outside Tammi.
- Agents are **pluggable**. Each Agent Definition has a name, a command and arguments. Supported Agents have
  built-in definitions; Custom Agents are configured much like a custom tool.
- **Working directory:** an agent starts in the Workspace Folder by default, so it can see and change
  every repository. The user can start it in a single Workspace Worktree instead.
- **Task context:** Tammi writes the workspace's task details (title, description, links) to a markdown
  context file in `.tammi/` and keeps it up to date. When an agent starts, Tammi sends it an
  **initial prompt** telling it about the context file. The initial prompt is configurable, with a
  placeholder for the context file's path. Each Agent Definition says how the prompt is passed to its CLI.
  - The context file is `.tammi/tasks.md`. It has a section for each task, in the workspace's order: its key and
    title, a link to its page, its Task Status (with its category), its repository's page if it belongs to one, and
    its description as the source gives it (markdown from GitHub, wiki markup from JIRA). A workspace without tasks
    gets a file that says so.
  - It's written each time the workspace's tasks have all been read (see [Tasks section](#workspace-view-tab)): when
    the tab opens, on Refresh, when integrations are added or removed, and when the task list changes. It's only
    rewritten when its contents change, and always in full, so an agent never reads half a file. A task that can't be
    read is listed by its key, with why, and its earlier details aren't kept. If the file can't be written, the Tasks
    section says why.
  - Starting an agent that takes an initial prompt writes the file first if it doesn't exist yet (e.g. while the tasks
    are still being read), listing the tasks by their keys until their details are read. If it can't be written, the
    agent doesn't start, and says why. An agent that doesn't take an initial prompt starts without it.
  - Running agents aren't told when the file changes.
  - The template's placeholder is `{contextFile}`, replaced with the file's full path. The rest of the template is
    sent as it is. Resumed Agent Sessions aren't sent the prompt again.
  - An Agent Definition has two lists of arguments. Its **arguments** are passed every time it starts. Its
    **initial prompt arguments** are added after them only when there's a prompt to send, with `{prompt}` where the
    prompt goes: `{prompt}` alone passes it as a positional argument, and `--prompt` then `{prompt}` as an option's
    value, so the option is left out with the prompt. A definition without initial prompt arguments doesn't take an
    initial prompt, and starts without one. Claude Code takes it as its positional argument.
  - The prompt is always passed as one argument, exactly as written. On Windows, a CLI that's a batch file (e.g.
    `claude.cmd`, installed with npm) runs through `cmd.exe`, so Tammi also escapes `cmd.exe`'s special characters.
    `cmd.exe` can't pass on line breaks, so a template with several lines can't be sent to a batch file: starting
    the agent fails and says why. The predefined templates are each a single line.
- **Claude Code** is the first Supported Agent. Others (e.g. Codex CLI, Gemini CLI) can be added as Custom Agents,
  and may become Supported Agents later.
- **Status** comes from the process (running, exited and exit code). Where an agent offers hooks or
  status events (e.g. Claude Code hooks), Tammi uses them for richer states. In keeping with the
  principles, hooks are supplied when Tammi launches the agent (e.g. through CLI arguments), never by
  changing repository files.
- **States**, shown on Home and in the Workspace View:

  | State | Meaning | Needs hooks |
  |-------|---------|-------------|
  | Working | The agent is busy | Yes |
  | Needs input | The agent is waiting on the user (e.g. a question or permission prompt) | Yes |
  | Idle | The agent has finished its turn | Yes |
  | Running | The process is running, but finer detail isn't available | No |
  | Exited | The process ended normally, or the user stopped it | No |
  | Error | The process crashed, was killed by something other than Tammi, or exited with a non-zero code | No |

  Agents with hooks use Working / Needs input / Idle while running. Agents without hooks show Running.
- **Hooks:** each Agent Definition says whether its agent has hooks, and how they're supplied and read. Only Supported
  Agents have them for now. Custom Agents have none, and show Running.
  - Tammi runs a small HTTP server on the loopback address (`127.0.0.1`, on a port chosen when it's first needed).
    Hooks POST each event to it as JSON. Each agent gets its own random token, passed in its environment as
    `TAMMI_AGENT_HOOK_TOKEN` rather than its arguments, so other users can't read it from the process list. The hooks
    send it as a bearer token, and events with an unknown token are refused. The token stops working when the agent
    ends.
  - Tammi answers each event with an empty response, so hooks never change what the agent does. If Tammi can't be
    reached, the agent carries on without it. If the server can't start, agents start without hooks and show Running.
  - An agent with hooks shows Running until its first event.
  - **Claude Code** gets HTTP hooks in its `--settings` argument, which it merges with the hooks in its own settings
    files. Nothing is written to disk. Its events map to states like this:

    | Event | State |
    |-------|-------|
    | `UserPromptSubmit`, `PostToolUse`, `PostToolUseFailure`, `PermissionDenied`, `ElicitationResult` | Working |
    | `PermissionRequest`, `Elicitation`, `PreToolUse` for `AskUserQuestion` and `ExitPlanMode`, `Notification` for a permission prompt or MCP elicitation | Needs input |
    | `Stop`, `StopFailure`, `Notification` for `idle_prompt` | Idle |

    Claude Code doesn't report some moments, so the state can lag: before the first prompt (e.g. on its folder trust
    dialog) it shows Running, after the user answers a permission prompt it shows Needs input until the tool finishes,
    and after the user interrupts a turn it shows Working until Claude Code reports it idle a minute later. Hooks
    don't run where Claude Code's settings turn them off (e.g. `disableAllHooks`, managed `allowManagedHooksOnly`, or
    an `allowedHttpHookUrls` list without Tammi's URL), and the agent then shows Running.
  Stopping an agent hangs up its terminal and kills the process if it doesn't exit, so the exit code it then
  gets doesn't mean it failed: a stopped agent is Exited, whatever its exit code.
- **Lifetime:**
  - Closing a workspace tab doesn't stop its terminals or agents. They keep running in the background,
    their status still shows on Home, and reopening the tab reattaches to them.
  - Closing Tammi warns if any agents are running, saying how many, and the user can cancel. Then it ends all
    terminal and agent processes. Running terminals alone don't warn.
  - **Agent Sessions can be resumed across app restarts.** Tammi records each Agent Session (its ID, the
    agent's name, the folder it was started in and when it was last started) in `.tammi/agent-sessions.json`.
    Later, the user can return to it using the agent's own resume command (e.g. `claude --resume <session-id>`).
    - Tammi gets the session ID in either or both of two ways:
      - **Setting it at launch:** Tammi makes up a UUID and passes it with the Agent Definition's **session ID
        arguments**, with `{sessionId}` where the ID goes (Claude Code: `--session-id {sessionId}`).
      - **From the agent's hooks**, when they report it. Claude Code's hook events all carry the session's ID, so
        Tammi follows the agent when it moves to another session in the same process (e.g. after `/clear`) and
        records the new session too. Only Supported Agents have hooks, so Custom Agents need session ID arguments.
    - The definition's **resume arguments** resume a session (Claude Code: `--resume {sessionId}`). They're added
      after its arguments in place of the session ID arguments and the initial prompt, which the session already
      has. The agent resumes in the folder the session was started in.
    - An agent's sessions are recorded only when its definition has resume arguments and a way to get the ID. Custom
      Agents have neither list unless the user sets them.
    - A session is resumed with the Agent Definition that has its agent's name now. If none has, or that definition
      can no longer resume sessions, the session is still listed but can't be resumed.
    - Only session IDs that Tammi could have made (UUIDs) are resumed, so a changed file can't pass the agent other
      arguments.
    - The file is versioned like `settings.json`. If it can't be read, no sessions are listed, and Tammi keeps a copy
      of it as `agent-sessions.invalid.json` before it next changes the file.
    - The user can forget a session, so it's no longer offered.
  - Plain terminals aren't restored after a restart.
  - **Agent and Git Graph tabs are reopened after a restart.** Tammi keeps the agents and Git Graphs open in each
    workspace in `.tammi/open-tabs.json`, updating it as they open and close (not as Tammi closes and ends every agent).
    The first time a workspace is opened after Tammi starts, its agents are resumed from their recorded Agent
    Sessions and its Git Graphs are opened, in their old order. An agent whose session can't be resumed, or a graph
    whose repository is no longer in the workspace, isn't reopened, and the view says so. Agents without a recorded
    session aren't kept. Terminals are left out, as keeping them would depend on the terminal supporting it.

## Integrations

### GitHub

- Provides **tasks** (issues) and **repositories**. It works with github.com and with GitHub Enterprise Server,
  given its API URL.
- **Authentication:** a personal access token (fine-grained, or classic with the `repo` scope for private
  repositories), kept in the credential store. Fine-grained tokens need read access to issues, contents and metadata
  on the repositories to include: GitHub's issue search leaves out private repositories' issues unless the token can
  read their contents. A fine-grained token belongs to one account or organisation, so an organisation's private
  repositories need a token owned by that organisation (or a classic token).
  - For github.com, Settings links to GitHub's new fine-grained token page with the token's name, description and
    read-only Issues, Contents and Metadata permissions filled in. GitHub can't fill in the owner or repository access,
    so the user chooses them. An Enterprise Server's link opens its token settings, because servers before 3.20 can't
    fill in the details.
  - GitHub has no way to list a fine-grained token's permissions, so when a fine-grained token is saved or tested,
    Tammi reads the latest commit of one private repository the token reaches. If that's refused, the integration is
    still connected but shows a warning that private issues won't appear in searches until the token can read
    Contents. Repositories the token wasn't given at all can't be detected, and classic tokens aren't checked.
  - **SSH keys:** to add SSH keys from Settings → Git, a fine-grained token also needs read and write access to the
    account's Git SSH keys (classic tokens: `write:public_key`). Tammi doesn't ask for it by default, so the token
    link leaves it out. Without it, the key's status is unknown, and adding it opens GitHub's page for adding a key with
    the public key on the clipboard. A key GitHub says is already in use counts as added if it's on the user's account.
- **Task keys** are GitHub's issue references, `owner/repo#number`. Pull requests aren't tasks.
- **Finding tasks:**
  - **Assigned:** the user's open issues (`is:open assignee:@me archived:false`). GitHub has no in-progress state, so
    every open issue counts as still to do.
  - **Text search** covers the open issues (`state:open`) in the user's own repositories, the organisations that own
    repositories they can reach (`org:`), and other users' repositories they collaborate on (`repo:`, so those users'
    other repositories aren't searched). Tammi reads these from the user's repositories and remembers them for 10 minutes. A search that covers
    many accounts and repositories is split into several requests. GitHub's search qualifiers (e.g. `label:bug`) can
    still be added to the text.
  - A task key or an issue's URL finds that issue, in any repository the token can read.
- **Repositories** are those the user owns, collaborates on or reaches through an organisation. Each has its HTTPS and
  SSH clone URLs.
- **Rate limits:** when GitHub asks Tammi to wait a few seconds, it waits and retries. For longer waits, the request
  fails and says when to try again.

### JIRA

- Provides **tasks** (issues). It works with JIRA Cloud and with JIRA Server and Data Center, given the site's URL.
  Each JIRA site is a separate integration.
- **Authentication:**
  - **JIRA Cloud:** the Atlassian account's email address (a setting) and an API token (kept in the credential store).
    The token can be one without scopes, or one with the JIRA scopes `read:jira-work` and `read:jira-user`. Tokens
    with scopes only work through Atlassian's API gateway (`api.atlassian.com/ex/jira/<cloud ID>`), so when the site
    rejects a token, Tammi reads the site's cloud ID (`_edge/tenant_info`) and tries the gateway, then keeps using
    whichever worked. Only reading the user (`myself`) is rejected at the site: searches and issues answer as if
    the request were anonymous, so a private project shows nothing. So before anything else, Tammi reads the user to
    find which route a token works on, and does so again for a new token. When the gateway says a scope is missing,
    Tammi names the scopes it needs.
  - **Server and Data Center:** a personal access token (JIRA 8.14 or later), kept in the credential store. When
    JIRA wants a CAPTCHA solved after failed logins, Tammi asks the user to log in to JIRA in a browser.
- **Task keys** are JIRA's issue keys, e.g. `ABC-123`. A moved issue is read with its new key.
- **Finding tasks:**
  - **Assigned:** the user's issues that are still to do
    (`assignee = currentUser() AND statusCategory = "To Do" ORDER BY updated DESC`).
  - **Text search** covers the site's issues that the user can see and that aren't done
    (`text ~ "<words>" AND statusCategory != "Done" ORDER BY updated DESC`), since a JIRA site is the
    organisation's own. Characters that JQL treats specially are left out of the words.
  - An issue key, or a URL on the site that shows an issue (its page, or a board or search with `selectedIssue`),
    finds that issue.
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
- **App data** lives in `%LOCALAPPDATA%\Tammi\{Config,Data,Cache,Logs}` on Windows, and in the XDG base
  directories on Linux (`~/.config/tammi`, `~/.local/share/tammi`, `~/.cache/tammi` and `~/.local/state/tammi/logs`
  by default).
- **Logging:** Tammi logs to the console and to a log file for each day in the Logs folder, through Serilog. It keeps
  the last 7 days' files, and starts a new file when one reaches 10 MB. The framework's own messages are only logged
  from Warning up, unless the log level is Trace.
- **Window state:** Tammi opens its window at the size it had when it was last closed, maximised if it was then.
  The state is kept in `window-state.json` in the Data folder, apart from the settings. If it's missing or can't be
  read, the window opens at its default size.
- **Layout state:** the sizes the user gives parts of the window (so far, the width of a Git Graph's commit details and
  the height of its message body) are kept in `layout-state.json` in the Data folder, apart from the window state,
  which the host writes as the window closes. It's written each time the user finishes resizing. If it's missing or
  can't be read, the parts have their default sizes.
- **Credentials** for integrations live in the platform secret store, never in settings or data files:
  - **Windows:** Windows Credential Manager, as generic credentials named `Tammi:<key>`, saved for the local
    machine only.
  - **Linux:** the Secret Service (e.g. GNOME Keyring or KWallet), through libsecret. libsecret is a system
    library and isn't shipped with Tammi. When libsecret is missing or no Secret Service is running, Tammi
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
  - **Windows:** ConPTY (Windows 10 1809 or later). The process runs in a Job Object, so closing the terminal can
    end every process it started (e.g. a long-running command), not just the shell. Closing the pseudoconsole hangs
    them up, and the job is ended if they haven't exited a few seconds later.
  - **Linux:** a `/dev/ptmx` pseudoterminal. The process is started with `posix_spawn` rather than `forkpty`,
    because a .NET process can't safely run code in a forked child before `exec`. `posix_spawn` starts the new
    session and opens the terminal without any code in the child. This needs glibc 2.29 or later.
  - xterm.js is vendored in the UI library (`wwwroot/lib/xterm`), so the app needs no network access or npm build.

## Open Questions

None at the moment. Add new questions here. When one is resolved, record the decision in the sections
above and remove the question.

