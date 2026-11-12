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

  terminal.attachCustomKeyEventHandler(event => handleClipboardKeys(terminal, event));
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
 * Copy and paste keys. Ctrl+Shift+C and Ctrl+Shift+V work everywhere. On Windows, as in Windows Terminal, Ctrl+C
 * also copies when text is selected (otherwise it interrupts), and Ctrl+V pastes.
 * @returns {boolean} false when the key was handled here, so xterm.js doesn't send it to the process.
 */
function handleClipboardKeys(terminal, event) {
  if (event.type !== 'keydown' || !event.ctrlKey || event.altKey || event.metaKey) {
    return true;
  }

  // Ctrl+Enter. Terminals can't tell it from Enter, so send a line feed, which programs such as Claude Code treat as
  // a newline rather than a submit.
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    terminal.input('\n');
    return false;
  }

  // Ctrl+Delete deletes the next word. Shells don't bind xterm.js's sequence for it, but all bind Alt+D (ESC d).
  if (event.key === 'Delete' && !event.shiftKey) {
    event.preventDefault();
    terminal.input('\x1bd');
    return false;
  }

  // Ctrl+Backspace deletes the previous word. xterm.js sends ^H, which shells treat as a plain Backspace. Alt+Backspace
  // (ESC DEL) is the word deletion that shells bind.
  if (event.key === 'Backspace' && !event.shiftKey) {
    event.preventDefault();
    terminal.input('\x1b\x7f');
    return false;
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
