# Tammi

Task-first software development workspace manager, built with .NET MAUI Blazor Hybrid (Windows) and Photino.Blazor (Linux) sharing one Blazor UI.

Read `docs/CONTEXT.md` before starting work. It defines the product, the glossary and the open questions.
Use its glossary terms in code and UI. When a decision resolves an open question, update that document.

## Code style

Put each top-level type in its own file, named after the type (e.g. `FolderPickerOptions.cs`). Nested types can stay
in their containing type's file.

## Token scopes and permissions

Wherever the UI names the scopes or permissions an integration's token needs (e.g. the token help in Settings →
Integrations), show them as a bulleted list in alphabetical order, one to a line, with scope names in `<code>`. Keep the
lists in messages and docs in the same order. Don't run them together in a sentence. Separate lists for fine-grained
and classic tokens, or for optional extras, are fine, each in alphabetical order.

## Third-party software

Every third-party library that ships with the app must be listed on the About page's third-party software list.
When you add one (including a new transitive dependency that ships, or a dependency of only one host), add an entry
to `src/Deosrc.Tammi.UI/About/ThirdPartySoftware.cs` in alphabetical order, and add its license text to
`src/Deosrc.Tammi.UI/Licenses/`, copied from the package or its source repository. If the license is only published online,
link to it instead. When you remove a library, remove its entry and any license text no other entry uses.

## UI

The UI uses Fluent UI Blazor components and Fluent icons, styled with Fluent's tokens (`--colorNeutral*`, `--spacing*`,
`--fontSize*`). Keep custom markup only where Fluent can't do what's needed, and say why in a comment (e.g. the git
graph's virtualised commit table, its branch and tag labels, the splitters, the xterm.js terminal and brand logos).

- Reuse the shared components rather than building another: `SidePanel`, `SidePanelSection`, `SidePanelCard`,
  `SidePanelListItem`, `PanelMessage` (errors and confirmations in a panel), `PointMenu` (menus opened at the pointer,
  e.g. by a right-click), `ToggleButtonGroup`, `Splitter`, `StatusDot`, `GitSyncChips` and `CopyOnClick` (text copied
  by clicking it).
- Text can't be selected, except in fields and terminals. Mark text that's worth selecting `selectable` (e.g. a
  diff's lines, a commit's message, a command, a path in settings). Wrap an error or warning message (but not a
  confirmation with buttons), or other text to copy whole (e.g. a version or a scope), in `CopyOnClick` instead.
  `PanelMessage` does this for errors.
- Badges are filled for counters and tinted for everything else.
- Page and tab titles are in title case.

Pitfalls:

- A component's scoped CSS doesn't reliably reach the root element of a child component, such as a Fluent component.
  Use `::deep` under an element the component renders itself, or put the rule in `app.css`.
- Fluent's default stylesheet loads after `app.css` and wins over plain element selectors (e.g. `h2`). Style headings
  through a class or a more specific selector.
- `FluentMessageBar` and `FluentBadge` are `width: 100%`-style hosts: side margins make a message bar overflow, so give
  it `width: auto`. A badge's icon is white, for filled badges: a tinted badge with an icon needs
  `fill: currentColor !important` on it.
- `FluentMultiSplitter` reports sizes only as percentages, and its `Min` and `Max` take only `px` or `%`. Use `Splitter`
  for pixel sizes, keyboard resizing and double-click reset.
- Fluent's `Autofocus` throws, and ends the UI, when the input has gone before it's focused, e.g. Home's search when
  the app starts and goes straight to the last tab. Use `AutofocusTextInput` instead of `FluentTextInput` with it.
- WebKitGTK (the Linux web view) ignores an unprefixed `user-select`: write `-webkit-user-select` beside it.
- A menu opened by a right-click closes as soon as the button is let go unless the pointer is inside it. `PointMenu`
  places the menu so it is.

To check a layout without the app, render Fluent's web components in headless Chromium (e.g. Playwright's cached
build): serve the package's `Microsoft.FluentUI.AspNetCore.Components.lib.module.js` over a local HTTP server (a module
won't load from `file://`), import it and call its `beforeWebStart`, `afterWebStarted` and `afterStarted` exports, then
screenshot the page with `app.css` and the markup under test.
