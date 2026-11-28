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
 * Ctrl+C also copies when text is selected (otherwise it interrupts), and Ctrl+V pastes. Then a few Ctrl keys are
 * translated (see translateKey).
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

  return translateKey(terminal, event, keyboardProtocol(terminal));
}

/**
 * The keyboard protocol the program has asked for, which sends keys with all their modifiers: 'win32' for
 * win32-input-mode (ConPTY asks for it on Windows builds that support it), 'kitty' for the kitty keyboard protocol, or
 * null for legacy input.
 */
function keyboardProtocol(terminal) {
  if (terminal.modes.win32InputMode) {
    return 'win32';
  }

  // xterm.js doesn't expose the kitty keyboard flags, so they're read from its core, which its own `modes` getter reads
  // too. The vendored version is pinned (lib/xterm/README.md): check this still works when it's updated.
  return (terminal._core?.coreService?.kittyKeyboard?.flags ?? 0) !== 0 ? 'kitty' : null;
}

/**
 * Ctrl keys sent as other keys, where the program wouldn't act on the key as pressed. A keyboard protocol sends keys as
 * they're pressed, so most need this only with legacy input (e.g. bash and zsh), which can't encode them. Two are
 * translated even then, because Claude Code (2.1) doesn't bind them, however they're encoded.
 * @param {'win32' | 'kitty' | null} protocol The program's keyboard protocol (see keyboardProtocol).
 * @returns {boolean} false when the key was translated, so xterm.js doesn't send it to the process.
 */
function translateKey(terminal, event, protocol) {
  if (event.shiftKey) {
    return true;
  }

  // Ctrl+Delete deletes the next word. As pressed, it's a plain Delete in Claude Code, and legacy input sends a
  // sequence shells don't bind. Alt+D (ESC d) deletes the next word in bash, zsh, fish, PowerShell and Claude Code.
  if (event.key === 'Delete') {
    event.preventDefault();
    terminal.input('\x1bd');
    return false;
  }

  // Ctrl+Enter. Claude Code ignores Enter with Ctrl, and legacy input can't tell it from Enter, so send a line feed,
  // which programs such as Claude Code treat as a newline rather than a submit. In win32-input-mode the key goes as
  // pressed: ConPTY turns it into a line feed itself for programs that read terminal input, such as Claude Code, and
  // console programs such as PowerShell get the real key.
  if (event.key === 'Enter' && protocol !== 'win32') {
    event.preventDefault();
    terminal.input('\n');
    return false;
  }

  // Ctrl+Backspace deletes the previous word. With legacy input xterm.js sends ^H, which shells treat as a plain
  // Backspace, so send Alt+Backspace (ESC DEL), the word deletion that shells bind. With a protocol the key goes as
  // pressed: PowerShell only binds Ctrl+Backspace, not Alt+Backspace.
  if (event.key === 'Backspace' && protocol === null) {
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
