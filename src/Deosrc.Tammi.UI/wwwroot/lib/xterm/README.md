# xterm.js

The terminal emulator used by the `Terminal` component, copied from these npm packages, with the `.mjs` files renamed to `.js` (see below):

| File | Package |
|------|---------|
| `xterm.js`, `xterm.css` | [`@xterm/xterm`](https://www.npmjs.com/package/@xterm/xterm) 6.0.0 (`lib/xterm.mjs`, `css/xterm.css`) |
| `addon-fit.js` | [`@xterm/addon-fit`](https://www.npmjs.com/package/@xterm/addon-fit) 0.11.0 (`lib/addon-fit.mjs`) |
| `addon-unicode11.js` | [`@xterm/addon-unicode11`](https://www.npmjs.com/package/@xterm/addon-unicode11) 0.9.0 (`lib/addon-unicode11.mjs`) |
| `addon-web-links.js` | [`@xterm/addon-web-links`](https://www.npmjs.com/package/@xterm/addon-web-links) 0.12.0 (`lib/addon-web-links.mjs`) |

They're the packages' ES module builds. The source maps are left out to keep the repository small.

The `.mjs` files are renamed to `.js` because the Windows host's web view (MAUI's `BlazorWebView`) serves `.mjs` files as
`application/octet-stream`, and browsers refuse to load a module with that type. `.js` files are served as JavaScript.
Their contents are unchanged, and they're still loaded as ES modules by `import`.

All four are MIT licensed by the xterm.js authors (see `LICENSE`). To update them, download the new versions from
npm, copy the same files (renaming `.mjs` to `.js`) and update this table.
