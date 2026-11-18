# Plan: Migrate TAMMi UI to Fluent UI (deosrc/Tammi#172)

## Context

The UI in `src/Deosrc.Tammi.UI` is entirely custom: one 4,819-line global `app.css`, ~85 hand-drawn inline SVG icons, hand-built tabs/menus/dialogs/forms, and no component library. The goal is to move to Fluent UI Blazor (https://www.fluentui-blazor.net) and Fluent icons (https://fluenticons.co/), using native Fluent functionality wherever possible and deleting custom code once unused.

Working rules:
- Migrate one section at a time, each as its own commit(s) on branch `Tammi-172-migrate-to-fluent-ui`.
- Check each section in the running app before moving on (only Core has tests; there are no UI tests).
- Remove unused CSS/logic/JS in the same commit that makes it unused.
- Build re-usable components (with their styling) when a second section needs the same pattern; don't design them speculatively.
- Order: top-down (foundation, layout, then sections), with shared components built bottom-up as needed.

## App facts that shape the plan

- Blazor Hybrid desktop: Photino (Linux) and MAUI (Windows) hosts, shared Razor class library `src/Deosrc.Tammi.UI` (net10.0). No Fluent package yet.
- Both hosts have a near-identical `wwwroot/index.html`; Fluent CSS/JS must be added to both.
- Service registration: `UI/ServiceCollectionExtensions.cs` (`AddTammiUI`). Layouts: `UI/Layout/MainLayout.razor`, `SettingsLayout.razor`. Tabs: `UI/Layout/TabStrip.razor`.
- Theme: dark/light variables defined three times (`app.css`, `palette.css`), driven by `ThemeSynchroniser` setting `data-theme`. Core's `ISystemTheme` remains the data source.
- `AGENTS.md` rule: new third-party packages must be listed in `UI/About/ThirdPartySoftware.cs` with a licence file in `UI/Licenses/*.txt`.
- Build/run (Linux): `dotnet build Deosrc.Tammi.Linux.slnf`; `dotnet run --project src/Deosrc.Tammi.Linux`; hot reload `scripts/watch-linux.sh`. `TreatWarningsAsErrors` is on.

## Steps (each is a commit or small group; tick when verified)

- [x] **1. Foundation.** (Done and checked on Linux by the user: Fluent assets load, and the theme follows Tammi's setting. Still to check: the Windows MAUI host. `ThemeSynchroniser` now also calls Fluent's `IThemeService.SetThemeAsync` with the resolved theme, as WebKitGTK's own system-theme detection disagrees with the desktop's; step 2 must keep that behaviour.) Uses Fluent UI Blazor **v5.1.0** (current stable; v5 is based on Fluent Web Components v3, so its tokens and theming API differ from v4 - check the v5 docs before step 2). Needed SDK 10.0.404 per `global.json`. Add `Microsoft.FluentUI.AspNetCore.Components` and the Icons package (central versions in `Directory.Packages.props`). Call `AddFluentUIComponents()` in `AddTammiUI`. Add Fluent CSS/JS and provider components (`FluentProviders` or equivalent) to both `index.html` files and `Routes`/`MainLayout`. Add third-party listing and licence files. Verify Fluent static assets load under Photino's file provider (`StaticWebAssetsFileProvider`) before continuing.
- [ ] **2. Theme.** Replace `ThemeSynchroniser` with `FluentDesignTheme` bound to the theme preference and `ISystemTheme`. Keep system-theme fallback behaviour.
  - **Source of truth is the brand palette in `palette.css`** (`--tammi-slate`, `-slate-raised`, `-mint`, `-teal`, `-white`, `-grey`, `-grey-dark`). Fluent tokens are mapped from these; the brand variables stay.
  - Mapping (names below are v4-style guesses; v5 differs, so re-derive from the v5 token/theme docs first): accent = teal in light, mint in dark (accent differs per mode, so set it per mode rather than one `CustomColor`); neutral layer/background = white (light) / slate (dark); raised surface (cards, panels) = `#fff` / slate-raised; foreground = slate / white; muted text = grey-dark / grey; text on accent = white / slate.
  - Existing `--color-*` variables are bridged to Fluent tokens (`--color-surface: var(--neutral-layer-2)` etc.) so unmigrated sections keep following the theme; the bridge is deleted in step 12. Non-brand colours (error, success, warning, merged, git graph lanes, jira, claude) stay as they are.
  - Delete the triplicated dark-mode blocks in `app.css` and the unused `--tammi-bg/-surface/-text/-accent*` theme tokens in `palette.css` (keep the brand colours and mark tints).
- [ ] **3. Layout shell.** `MainLayout` header/footer, `UsageFooter` (`UsageBar` to `FluentProgress`), `UpdatePrompt`, `#blazor-error-ui` (must still work before Blazor boots; keep both `index.html` in sync), `GitMissingDialog` to `FluentDialog`. Keep `FocusOnNavigate Selector="h1"` working.
- [ ] **4. Tab strip and nav.** Keep the custom closable/reorderable `TabStrip` (decided) and restyle visuals only: Fluent tokens, `FluentButton` for close/icon buttons, Fluent icons; no structural or behaviour changes. Migrate `SettingsLayout` nav to `FluentNavMenu`. Remove superseded `.tab*` / `.settings-nav` CSS.
- [ ] **5. Shared building blocks (as first needed in Settings).** `SettingsField` (label + control + help text), button variants to `FluentButton` (`Appearance`), menus to `FluentMenu` (removes per-page `_xMenuOpen` bools and `.menu-backdrop`), `Switch` to `FluentSwitch`, inline alerts/error/warning text to `FluentMessageBar`, badges/chips to `FluentBadge`. Each new shared component gets bUnit tests in a new `tests/Deosrc.Tammi.UI.Tests` project (add to `Deosrc.Tammi.slnx` and the Linux/Windows slnf filters, central package versions in `Directory.Packages.props`, add to CI test step, check whether the third-party listing rule applies to test-only packages).
- [ ] **6. Settings pages**, simplest first: `About`, `Git` (`GitAuthor`, `GitGraphSettings`), `General`, `Updates`, `SshKeys`, `Repositories` (+ `RepositoryCatalogueTable`), `Agents`, `Tools`, `Integrations`, `ThirdParty`. Delete `.settings-*` CSS as each page moves.
- [ ] **7. Home and Repositories.** Workspace cards to `FluentCard`; Home tab group; catalogue tables to `FluentDataGrid` (retire `column-resize.js` and `.data-table*` CSS if it covers resizing); `SortableList` stays custom unless Fluent offers a fit.
- [ ] **8. New / Edit Workspace.** Wizard steps, `TaskPicker`, `RepositoryPicker`, progress bars, `DeleteWorkspace` confirmation to `FluentDialog`.
- [ ] **9. Workspace and RepositoryGit pages.** Side panel (`SidePanelSection` to `FluentAccordion`), session tabs, status chips, PR badges, agent indicators, `ContextMenu` to `FluentMenu` (remove `ContextMenu.razor` + JS).
- [ ] **10. Git graph / diff / terminal.** Migrate toolbar buttons, menus, popovers and inline confirmations, and also replace custom parts with Fluent equivalents wherever one fits (e.g. `FluentMultiSplitter` for the JS splitters in `GitGraphView`, `GitCommitDetails`, `GitWorkingChanges`; Fluent tooltips, menus for the context menu). Assess each (graph rows/lanes, diff view, virtualised commit table, resizable columns, xterm terminal) and keep custom only where Fluent can't match the behaviour, recording why here. Decided: the virtualised commit table and its resizable columns stay custom (restyle with Fluent tokens/icons only) if Fluent can't replace them; no `FluentDataGrid` prototype. Delete the corresponding JS/CSS when replaced.
- [ ] **11. Icons.** Replace inline line-icon SVGs with Fluent icons across components; keep brand/service logos (`IntegrationIcon`, `AgentIcon`) and wordmarks. Remove `.icon` CSS when unused. (May be done per-section in steps 3-10 instead; prefer that, leaving this as the sweep for stragglers.)
- [ ] **12. Final cleanup.** Remove remaining dead rules from `app.css`, unused JS (`clipboard.js` duplication in `GitCommitDetails.razor.js`), `.spinner`, and any orphaned components. Confirm `app.css` only holds styles for the intentionally custom parts.

## Per-step checklist

1. Build: `dotnet build Deosrc.Tammi.Linux.slnf` (warnings are errors).
2. Core tests still pass: `dotnet test --project tests/Deosrc.Tammi.Core.Tests`.
3. Run the app and check the migrated section in light and dark themes (manual; results to be recorded only once actually verified).
4. Grep for now-unused CSS classes/components/JS from the migrated section and delete them in the same commit.

## Open decisions

- ~~Closable/reorderable app `TabStrip`~~: decided to keep it custom and restyle it with Fluent tokens and icons.
- ~~Start order~~: decided to do the tab strip first; already satisfied (step 4 precedes shared components and Settings).
- ~~bUnit~~: decided to add bUnit tests for the new shared components in a new `tests/Deosrc.Tammi.UI.Tests` project (see step 5).
- ~~Theme~~: decided to map Fluent tokens from the `--tammi-*` brand variables (see step 2).
- ~~Git graph, diff view, terminal, splitters~~: decided to migrate to Fluent where possible (see step 10); keep custom only where Fluent can't match behaviour.
- Whether Fluent assets work on Windows MAUI WebView as well as Photino (Windows CI is disabled).

## Critical files

- `src/Deosrc.Tammi.UI/ServiceCollectionExtensions.cs`, `Routes.razor`, `Layout/*`, `Components/*`, `Pages/**`, `Workspaces/**`
- `src/Deosrc.Tammi.UI/wwwroot/css/app.css`, `palette.css`
- `src/Deosrc.Tammi.Linux/wwwroot/index.html`, `src/Deosrc.Tammi.Windows/wwwroot/index.html`
- `Directory.Packages.props`, `src/Deosrc.Tammi.UI/About/ThirdPartySoftware.cs`, `src/Deosrc.Tammi.UI/Licenses/`
