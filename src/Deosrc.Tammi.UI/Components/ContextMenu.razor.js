// Keeps a context menu inside the window: a menu opened near the right or bottom edge is moved back in, so it opens to
// the left of or above the pointer instead. Then it takes the focus, so Escape closes it.
export function fit(menu) {
    const margin = 4;
    const bounds = menu.getBoundingClientRect();
    if (bounds.right > window.innerWidth - margin) {
        menu.style.left = `${Math.max(margin, window.innerWidth - margin - bounds.width)}px`;
    }

    if (bounds.bottom > window.innerHeight - margin) {
        menu.style.top = `${Math.max(margin, window.innerHeight - margin - bounds.height)}px`;
    }

    menu.focus();
}
