// Makes the app bar's empty space act as the window's title bar: dragging it moves the window, and double-clicking it
// maximises or restores the window.

// Anything that does something of its own when clicked, so isn't empty space. The tabs are whole, not only their links,
// so the room around a tab's close button isn't either.
const interactive = [
    "a",
    "button",
    "input",
    "select",
    "textarea",
    "[role=button]",
    "[role=menu]",
    "[role=menuitem]",
    "[popover]",
    "fluent-button",
    "fluent-menu",
    "fluent-menu-list",
    "fluent-menu-item",
    ".tab-bar-tab",
    ".window-controls",
].join(", ");

// How far the pointer has to move with the button held before the window moves, so a click or a double-click doesn't.
const threshold = 4;

const isEmptySpace = (event) => event.button === 0 && !event.target.closest(interactive);

// Watches the bar. Returns an object to dispose when the bar goes.
export function attach(bar, dotNet) {
    let pressed = null;

    const down = (event) => {
        pressed = isEmptySpace(event) ? { x: event.screenX, y: event.screenY } : null;
        if (pressed) {
            // Stops the press from selecting text or starting to drag the wordmark's image.
            event.preventDefault();
        }
    };

    const move = (event) => {
        if (!pressed) {
            return;
        }

        if ((event.buttons & 1) === 0) {
            pressed = null;
            return;
        }

        if (Math.abs(event.screenX - pressed.x) >= threshold || Math.abs(event.screenY - pressed.y) >= threshold) {
            pressed = null;
            dotNet.invokeMethodAsync("BeginMove", Math.round(event.screenX), Math.round(event.screenY));
        }
    };

    const up = () => {
        pressed = null;
    };

    const doubleClick = (event) => {
        if (isEmptySpace(event)) {
            dotNet.invokeMethodAsync("ToggleMaximized");
        }
    };

    bar.addEventListener("mousedown", down);
    bar.addEventListener("dblclick", doubleClick);
    // The pointer may leave the bar before it has moved far enough.
    window.addEventListener("mousemove", move);
    window.addEventListener("mouseup", up);

    return {
        dispose() {
            bar.removeEventListener("mousedown", down);
            bar.removeEventListener("dblclick", doubleClick);
            window.removeEventListener("mousemove", move);
            window.removeEventListener("mouseup", up);
        },
    };
}
