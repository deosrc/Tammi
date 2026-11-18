// Tells the TabBar when its tabs overflow the room for them, and scrolls the tab shown into view.

// Watches the element that scrolls the tabs. Returns an object to dispose when the bar goes.
export function observe(scroller, dotNet) {
    let last = null;
    const check = () => {
        const overflowing = scroller.scrollWidth > scroller.clientWidth + 1;
        if (overflowing !== last) {
            last = overflowing;
            dotNet.invokeMethodAsync("SetOverflowing", overflowing);
        }
    };

    // The bar's size changes with the window, and the tabs' with tabs opening, closing and being renamed.
    const resize = new ResizeObserver(check);
    resize.observe(scroller);
    const mutation = new MutationObserver(check);
    mutation.observe(scroller, { childList: true, subtree: true, characterData: true });
    check();

    return {
        dispose() {
            resize.disconnect();
            mutation.disconnect();
        },
    };
}

// Scrolls the tab shown into view, if it's one of the tabs that scroll.
export function scrollActiveIntoView(scroller) {
    scroller.querySelector(".tab-bar-tab.active")?.scrollIntoView({ block: "nearest", inline: "nearest" });
}
