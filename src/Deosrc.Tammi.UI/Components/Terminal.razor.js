// The browser side of the Terminal component: an xterm.js terminal that sends input and size changes to .NET.
import { Terminal } from '../lib/xterm/xterm.js';
import { FitAddon } from '../lib/xterm/addon-fit.js';
import { Unicode11Addon } from '../lib/xterm/addon-unicode11.js';
import { WebLinksAddon } from '../lib/xterm/addon-web-links.js';

const isWindows = navigator.userAgent.includes('Windows');

const theme = {
  background: '#1e1e1e',
  foreground: '#cccccc',
  cursor: '#ffffff',
  selectionBackground: '#264f78',
  black: '#000000',
  red: '#cd3131',
  green: '#0dbc79',
  yellow: '#e5e510',
  blue: '#2472c8',
  magenta: '#bc3fbc',
  cyan: '#11a8cd',
  white: '#e5e5e5',
  brightBlack: '#666666',
  brightRed: '#f14c4c',
  brightGreen: '#23d18b',
  brightYellow: '#f5f543',
  brightBlue: '#3b8eea',
  brightMagenta: '#d670d6',
  brightCyan: '#29b8db',
  brightWhite: '#e5e5e5',
};

/**
 * Creates a terminal in an element, sized to fill it.
 * @param {HTMLElement} element The element to show the terminal in.
 * @param {DotNet.DotNetObject} dotNet The component, which receives OnInput, OnBinaryInput, OnResize and OpenLink.
 * @param {{ windowsBuildNumber?: number }} options Set windowsBuildNumber when the process runs in ConPTY.
 */
export function create(element, dotNet, options) {
  // Links must open in the user's browser, not in this web view, which would navigate away from Tammi.
  const openLink = (event, uri) => {
    event.preventDefault();
    dotNet.invokeMethodAsync('OpenLink', uri);
  };

  const terminal = new Terminal({
    // The Unicode 11 addon uses a proposed API.
    allowProposedApi: true,
    cursorBlink: true,
    fontFamily: '"Cascadia Mono", Consolas, "DejaVu Sans Mono", "Liberation Mono", "Ubuntu Mono", monospace',
    fontSize: 14,
    // Links that programs write as OSC 8 hyperlinks.
    linkHandler: { activate: openLink, allowNonHttpProtocols: false },
    scrollback: 10000,
    theme,
    // Keyboard protocols that send keys to programs with all their modifiers, once a program asks for one: ConPTY
    // asks for win32-input-mode (CSI ? 9001 h), so console programs such as PowerShell get the real key events, and
    // programs such as Claude Code and fish ask for the kitty keyboard protocol (CSI > flags u).
    vtExtensions: {
      win32InputMode: !!options.windowsBuildNumber,
      kittyKeyboard: !options.windowsBuildNumber,
    },
    windowsPty: options.windowsBuildNumber
      ? { backend: 'conpty', buildNumber: options.windowsBuildNumber }
      : undefined,
  });

  const fitAddon = new FitAddon();
  terminal.loadAddon(fitAddon);

  // Unicode 11 widths, so emoji and other wide characters take the two cells that programs expect.
  terminal.loadAddon(new Unicode11Addon());
  terminal.unicode.activeVersion = '11';

  // Plain URLs in the output.
  terminal.loadAddon(new WebLinksAddon(openLink));

  // The keys whose key down was handled here (by event.code). Their key up isn't sent either: in win32-input-mode
  // xterm.js sends key ups too, and the program would get the release of a key it never saw pressed.
  const handledKeys = new Set();
  terminal.attachCustomKeyEventHandler(event => {
    if (event.type === 'keyup' && handledKeys.delete(event.code)) {
      return false;
    }

    const send = handleKeys(terminal, event);
    if (!send && event.type === 'keydown') {
      handledKeys.add(event.code);
    }

    return send;
  });
  const subscriptions = [
    terminal.onData(data => dotNet.invokeMethodAsync('OnInput', data)),
    terminal.onBinary(data => dotNet.invokeMethodAsync('OnBinaryInput', data)),
    terminal.onResize(size => dotNet.invokeMethodAsync('OnResize', size.cols, size.rows)),
  ];

  terminal.open(element);

  // Right-click, as in Windows Terminal: copies the selection, or pastes when nothing is selected. It acts once, on
  // button down. The 'contextmenu' event can't be used for that, because web views raise it differently (on button
  // down, button up, or repeatedly while the button is held). It's only stopped, so the web view's own menu doesn't
  // show, and its copy can't reach xterm.js's selection.
  const onRightMouseDown = event => {
    if (event.button !== 2) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (terminal.hasSelection()) {
      copySelection(terminal, true);
    } else {
      paste(terminal);
    }
  };
  const suppressContextMenu = event => {
    event.preventDefault();
    event.stopPropagation();
  };
  element.addEventListener('mousedown', onRightMouseDown, true);
  element.addEventListener('contextmenu', suppressContextMenu, true);

  // Keep the terminal the size of its element. The PTY follows through onResize.
  const fit = () => {
    try {
      fitAddon.fit();
    } catch {
      // The element isn't laid out (e.g. it's hidden). It's fitted when it's shown, which resizes it.
    }
  };
  let frame = 0;
  const observer = new ResizeObserver(() => {
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(fit);
  });
  observer.observe(element);
  fit();

  return {
    /** Writes output (bytes, or a string). Resolves once xterm.js has processed it, which paces the output. */
    write: data => new Promise(resolve => terminal.write(data, resolve)),
    focus: () => terminal.focus(),
    getSize: () => ({ columns: terminal.cols, rows: terminal.rows }),
    dispose: () => {
      element.removeEventListener('mousedown', onRightMouseDown, true);
      element.removeEventListener('contextmenu', suppressContextMenu, true);
      observer.disconnect();
      cancelAnimationFrame(frame);
      subscriptions.forEach(subscription => subscription.dispose());
      terminal.dispose();
    },
  };
}

/**
 * The keys handled here rather than sent as xterm.js encodes them. Copy and paste come first, as they're the
 * terminal's own actions: Ctrl+Shift+C and Ctrl+Shift+V work everywhere, and on Windows, as in Windows Terminal,
 * Ctrl+C also copies when text is selected (otherwise it interrupts), and Ctrl+V pastes. Then, only for programs that
 * haven't asked for a keyboard protocol, a few Ctrl keys that legacy terminal input can't encode are translated.
 * @returns {boolean} false when the key was handled here, so xterm.js doesn't send it to the process.
 */
function handleKeys(terminal, event) {
  if (event.type !== 'keydown' || !event.ctrlKey || event.altKey || event.metaKey) {
    return true;
  }

  const key = event.key.toLowerCase();
  if (key === 'c' && (event.shiftKey || (isWindows && terminal.hasSelection()))) {
    event.preventDefault();
    copySelection(terminal);
    return false;
  }

  if (key === 'v' && (event.shiftKey || isWindows)) {
    event.preventDefault();
    paste(terminal);
    return false;
  }

  // A program that asked for a keyboard protocol gets every key as it was pressed, modifiers and all.
  if (keyboardProtocolActive(terminal)) {
    return true;
  }

  return translateLegacyKey(terminal, event);
}

/**
 * Whether the program has asked for a keyboard protocol that carries modifiers: win32-input-mode (ConPTY asks for it
 * on Windows builds that support it) or the kitty keyboard protocol.
 */
function keyboardProtocolActive(terminal) {
  if (terminal.modes.win32InputMode) {
    return true;
  }

  // xterm.js doesn't expose the kitty keyboard flags, so they're read from its core, which its own `modes` getter reads
  // too. The vendored version is pinned (lib/xterm/README.md): check this still works when it's updated.
  return (terminal._core?.coreService?.kittyKeyboard?.flags ?? 0) !== 0;
}

/**
 * Ctrl keys that legacy terminal input can't encode, translated to the keys that shells and Claude Code bind for the
 * same action. They're only a fallback, for programs such as bash and zsh that use legacy input, and on Windows builds
 * whose ConPTY doesn't ask for win32-input-mode: a guess at the binding can't suit every program.
 * @returns {boolean} false when the key was translated, so xterm.js doesn't send it to the process.
 */
function translateLegacyKey(terminal, event) {
  if (event.shiftKey) {
    return true;
  }

  // Ctrl+Enter. Terminals can't tell it from Enter, so send a line feed, which programs such as Claude Code treat as
  // a newline rather than a submit.
  if (event.key === 'Enter') {
    event.preventDefault();
    terminal.input('\n');
    return false;
  }

  // Ctrl+Delete deletes the next word. Shells don't bind xterm.js's sequence for it, but all bind Alt+D (ESC d).
  if (event.key === 'Delete') {
    event.preventDefault();
    terminal.input('\x1bd');
    return false;
  }

  // Ctrl+Backspace deletes the previous word. xterm.js sends ^H, which shells treat as a plain Backspace. Alt+Backspace
  // (ESC DEL) is the word deletion that shells bind.
  if (event.key === 'Backspace') {
    event.preventDefault();
    terminal.input('\x1b\x7f');
    return false;
  }

  return true;
}

function copySelection(terminal, clearAfter = false) {
  if (!terminal.hasSelection()) {
    return;
  }

  const copied = () => {
    if (isWindows || clearAfter) {
      terminal.clearSelection();
    }
  };

  // The Clipboard API needs a secure context. Where it's missing or refused, the 'copy' command makes xterm.js
  // put its selection on the clipboard instead.
  const copyCommand = () => {
    document.execCommand('copy');
    copied();
  };

  if (navigator.clipboard?.writeText) {
    navigator.clipboard.writeText(terminal.getSelection()).then(copied, copyCommand);
  } else {
    copyCommand();
  }
}

function paste(terminal) {
  if (!navigator.clipboard?.readText) {
    console.warn('The clipboard can\'t be read here.');
    return;
  }

  // paste() wraps the text in bracketed paste sequences when the program has asked for them.
  navigator.clipboard.readText()
    .then(text => terminal.paste(text))
    .catch(error => console.warn('Couldn\'t read the clipboard.', error));
}
