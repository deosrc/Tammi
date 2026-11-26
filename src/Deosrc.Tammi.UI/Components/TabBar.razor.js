// Fits the TabBar's closable tabs into the room for them: tabs that don't fit are hidden, and the tab shown never is.
// Tells the TabBar when any are hidden, so it offers a menu of them all. Also lets the user move the tabs marked
// movable, by dragging them or with Ctrl+Shift+Left and Right, and tells the TabBar where they put them.

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
    const reorder = allowReordering(scroller, dotNet);

    return {
        dispose() {
            resize.disconnect();
            mutation.disconnect();
            reorder.dispose();
        },
    };
}

// How far, in pixels, the pointer moves with the button held before it's a drag rather than a click.
const dragThreshold = 4;

// Runs a callback once the tabs have been put in their new order, which happens when the TabBar renders again, or
// after a while if it doesn't (e.g. the move was refused).
function afterReorder(scroller, callback) {
    let done = false;
    const finish = () => {
        if (!done) {
            done = true;
            observer.disconnect();
            clearTimeout(timeout);
            callback();
        }
    };
    const observer = new MutationObserver(finish);
    observer.observe(scroller, { childList: true });
    const timeout = setTimeout(finish, 500);
}

function allowReordering(scroller, dotNet) {
    const movable = (includeHidden) =>
        [...scroller.children].filter((tab) => tab.classList.contains("movable") && (includeHidden || !tab.hidden));

    // The drag under way: the tab, where it started, and where the tabs were when it did.
    let drag = null;

    const clear = (tabs) => {
        scroller.classList.remove("tab-bar-dragging");
        for (const tab of tabs) {
            tab.classList.remove("dragging");
            tab.style.transform = "";
        }
    };

    const end = (commit) => {
        if (!drag) {
            return;
        }

        const { tab, tabs, index, target, started, pointerId } = drag;
        drag = null;
        if (scroller.hasPointerCapture(pointerId)) {
            scroller.releasePointerCapture(pointerId);
        }

        if (!started) {
            return;
        }

        // The click that ends the drag doesn't go to the tab's address.
        const suppress = (event) => {
            event.preventDefault();
            event.stopPropagation();
        };
        window.addEventListener("click", suppress, { capture: true, once: true });
        setTimeout(() => window.removeEventListener("click", suppress, { capture: true }), 0);

        if (!commit || target === index || !tab.isConnected) {
            clear(tabs);
            return;
        }

        // The tabs stay where they were dropped until they're rendered in their new order, so they don't jump back
        // in between.
        scroller.classList.remove("tab-bar-dragging");
        const after = target > index;
        afterReorder(scroller, () => clear(tabs));
        dotNet.invokeMethodAsync("MoveTab", tab.dataset.key, tabs[target].dataset.key, after);
    };

    const onPointerDown = (event) => {
        if (event.button !== 0 || event.ctrlKey || event.shiftKey || event.metaKey || drag?.started) {
            return;
        }

        const tab = event.target.closest(".tab-bar-tab");
        if (!tab || tab.parentElement !== scroller || !tab.classList.contains("movable")
            || event.target.closest(".tab-bar-close")) {
            return;
        }

        drag = { tab, startX: event.clientX, pointerId: event.pointerId, started: false };
    };

    const onPointerMove = (event) => {
        if (!drag || event.pointerId !== drag.pointerId) {
            return;
        }

        const dx = event.clientX - drag.startX;
        if (!drag.started) {
            if (Math.abs(dx) < dragThreshold) {
                return;
            }

            const tabs = movable(false);
            const index = tabs.indexOf(drag.tab);
            if (index < 0 || tabs.length < 2) {
                drag = null;
                return;
            }

            Object.assign(drag, {
                started: true,
                tabs,
                index,
                target: index,
                rects: tabs.map((tab) => tab.getBoundingClientRect()),
            });
            scroller.setPointerCapture(drag.pointerId);
            scroller.classList.add("tab-bar-dragging");
            drag.tab.classList.add("dragging");
        }

        const { tab, tabs, index, rects } = drag;
        const own = rects[index];
        // The tab stays among the tabs that can be moved: it can't go before the first or after the last.
        const offset = Math.min(Math.max(dx, rects[0].left - own.left), rects[rects.length - 1].right - own.right);
        // A tab is passed once the dragged tab's leading edge crosses its middle. The dragged tab's own middle isn't
        // used: it stops with its edge at the first or last tab's, so its middle couldn't pass theirs if it's wider.
        let target = index;
        rects.forEach((rect, i) => {
            const middle = rect.left + rect.width / 2;
            if (i > index && own.right + offset > middle) {
                target++;
            } else if (i < index && own.left + offset < middle) {
                target--;
            }
        });

        // The tabs it has passed make room for it.
        tabs.forEach((other, i) => {
            let shift = 0;
            if (i > index && i <= target) {
                shift = -own.width;
            } else if (i < index && i >= target) {
                shift = own.width;
            }

            other.style.transform = other === tab ? `translateX(${offset}px)` : shift ? `translateX(${shift}px)` : "";
        });
        drag.target = target;
    };

    const onPointerUp = (event) => {
        if (drag && event.pointerId === drag.pointerId) {
            end(true);
        }
    };

    const onPointerCancel = (event) => {
        if (drag && event.pointerId === drag.pointerId) {
            end(false);
        }
    };

    const onKeyDown = (event) => {
        if (drag?.started && event.key === "Escape") {
            event.preventDefault();
            end(false);
            return;
        }

        if (!event.ctrlKey || !event.shiftKey || event.altKey || event.metaKey
            || (event.key !== "ArrowLeft" && event.key !== "ArrowRight")) {
            return;
        }

        const tab = event.target.closest?.(".tab-bar-tab");
        if (!tab || tab.parentElement !== scroller || !tab.classList.contains("movable")) {
            return;
        }

        event.preventDefault();
        const tabs = movable(true);
        const after = event.key === "ArrowRight";
        const neighbour = tabs[tabs.indexOf(tab) + (after ? 1 : -1)];
        if (!neighbour) {
            return;
        }

        // Moving the tab's element takes the focus from it, so it's given back.
        const key = tab.dataset.key;
        afterReorder(scroller, () => {
            const moved = [...scroller.children].find((child) => child.dataset.key === key);
            moved?.querySelector(".tab-bar-label")?.focus();
        });
        dotNet.invokeMethodAsync("MoveTab", key, neighbour.dataset.key, after);
    };

    // The browser's own drag of a link or its text would get in the way.
    const onDragStart = (event) => {
        if (event.target.closest?.(".tab-bar-tab.movable")) {
            event.preventDefault();
        }
    };

    scroller.addEventListener("pointerdown", onPointerDown);
    scroller.addEventListener("pointermove", onPointerMove);
    scroller.addEventListener("pointerup", onPointerUp);
    scroller.addEventListener("pointercancel", onPointerCancel);
    scroller.addEventListener("lostpointercapture", onPointerCancel);
    scroller.addEventListener("dragstart", onDragStart);
    // On the window, so Escape cancels a drag wherever the focus is.
    window.addEventListener("keydown", onKeyDown);

    return {
        dispose() {
            scroller.removeEventListener("pointerdown", onPointerDown);
            scroller.removeEventListener("pointermove", onPointerMove);
            scroller.removeEventListener("pointerup", onPointerUp);
            scroller.removeEventListener("pointercancel", onPointerCancel);
            scroller.removeEventListener("lostpointercapture", onPointerCancel);
            scroller.removeEventListener("dragstart", onDragStart);
            window.removeEventListener("keydown", onKeyDown);
        },
    };
}
