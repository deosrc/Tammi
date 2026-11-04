// The browser side of the Terminal component: an xterm.js terminal that sends input and size changes to .NET.
import { Terminal } from '../lib/xterm/xterm.mjs';
import { FitAddon } from '../lib/xterm/addon-fit.mjs';
import { Unicode11Addon } from '../lib/xterm/addon-unicode11.mjs';

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
 * @param {DotNet.DotNetObject} dotNet The component, which receives OnInput, OnBinaryInput and OnResize.
 * @param {{ windowsBuildNumber?: number }} options Set windowsBuildNumber when the process runs in ConPTY.
 */
export function create(element, dotNet, options) {
  const terminal = new Terminal({
    // The Unicode 11 addon uses a proposed API.
    allowProposedApi: true,
    cursorBlink: true,
    fontFamily: '"Cascadia Mono", Consolas, "DejaVu Sans Mono", "Liberation Mono", "Ubuntu Mono", monospace',
    fontSize: 14,
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

  terminal.attachCustomKeyEventHandler(event => handleClipboardKeys(terminal, event));
  const subscriptions = [
    terminal.onData(data => dotNet.invokeMethodAsync('OnInput', data)),
    terminal.onBinary(data => dotNet.invokeMethodAsync('OnBinaryInput', data)),
    terminal.onResize(size => dotNet.invokeMethodAsync('OnResize', size.cols, size.rows)),
  ];

  terminal.open(element);

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

function copySelection(terminal) {
  if (!terminal.hasSelection()) {
    return;
  }

  const copied = () => {
    if (isWindows) {
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
    console.warn('The clipboard can\'t be read here. Right-click the terminal and choose Paste.');
    return;
  }

  // paste() wraps the text in bracketed paste sequences when the program has asked for them.
  navigator.clipboard.readText()
    .then(text => terminal.paste(text))
    .catch(error => console.warn('Couldn\'t read the clipboard. Right-click the terminal and choose Paste.', error));
}
