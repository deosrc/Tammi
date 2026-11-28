# xterm.js

The terminal emulator used by the `Terminal` component, copied from these npm packages, with the `.mjs` files renamed to `.js` (see below):

| File | Package |
|------|---------|
| `xterm.js`, `xterm.css` | [`@xterm/xterm`](https://www.npmjs.com/package/@xterm/xterm) 6.1.0-beta.340 (`lib/xterm.mjs`, `css/xterm.css`) |
| `addon-fit.js` | [`@xterm/addon-fit`](https://www.npmjs.com/package/@xterm/addon-fit) 0.12.0-beta.336 (`lib/addon-fit.mjs`) |
| `addon-unicode11.js` | [`@xterm/addon-unicode11`](https://www.npmjs.com/package/@xterm/addon-unicode11) 0.10.0-beta.334 (`lib/addon-unicode11.mjs`) |
| `addon-web-links.js` | [`@xterm/addon-web-links`](https://www.npmjs.com/package/@xterm/addon-web-links) 0.13.0-beta.334 (`lib/addon-web-links.mjs`) |

They're the packages' ES module builds. The source maps are left out to keep the repository small.

They're beta builds because win32-input-mode and the kitty keyboard protocol, which pass keys to programs with their
modifiers (see `Terminal.razor.js`), aren't in a stable release yet (6.0.0 is the latest). Move to the stable release
that includes them once it's out. The addons are the betas built against the same core beta (their
`peerDependencies`).

The `.mjs` files are renamed to `.js` because the Windows host's web view (MAUI's `BlazorWebView`) serves `.mjs` files as
`application/octet-stream`, and browsers refuse to load a module with that type. `.js` files are served as JavaScript.
Their contents are unchanged, and they're still loaded as ES modules by `import`.

All four are MIT licensed by the xterm.js authors (see `LICENSE`). To update them, download the new versions from
npm, copy the same files (renaming `.mjs` to `.js`) and update this table.
