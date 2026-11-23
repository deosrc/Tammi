// Fits the TabBar's closable tabs into the room for them: tabs that don't fit are hidden, and the tab shown never is.
// Tells the TabBar when any are hidden, so it offers a menu of them all.

// Watches the element that holds the tabs. Returns an object to dispose when the bar goes.
export function observe(scroller, dotNet) {
    let last = null;
    const fit = () => {
        const tabs = [...scroller.children];

        // Measured with every tab shown: the scroller is as wide as its tabs, up to the room there is.
        for (const tab of tabs) {
            tab.hidden = false;
        }

        let hidden = false;
        if (scroller.scrollWidth > scroller.clientWidth + 1) {
            // The tab shown keeps its place, and its room. The others take what's left, in order, until one doesn't fit.
            let remaining = scroller.clientWidth;
            for (const tab of tabs) {
                if (tab.classList.contains("active")) {
                    remaining -= tab.offsetWidth;
                }
            }

            let full = false;
            for (const tab of tabs) {
                if (tab.classList.contains("active")) {
                    continue;
                }

                if (!full && tab.offsetWidth <= remaining) {
                    remaining -= tab.offsetWidth;
                } else {
                    full = true;
                    tab.hidden = true;
                    hidden = true;
                }
            }
        }

        if (hidden !== last) {
            last = hidden;
            dotNet.invokeMethodAsync("SetOverflowing", hidden);
        }
    };

    // The bar's size changes with the window, and the tabs' with tabs opening, closing and being renamed, or becoming
    // the one shown.
    const resize = new ResizeObserver(fit);
    resize.observe(scroller);
    resize.observe(scroller.parentElement);
    const mutation = new MutationObserver(fit);
    mutation.observe(scroller, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true,
        attributeFilter: ["class"],
    });
    fit();

    return {
        dispose() {
            resize.disconnect();
            mutation.disconnect();
        },
    };
}
