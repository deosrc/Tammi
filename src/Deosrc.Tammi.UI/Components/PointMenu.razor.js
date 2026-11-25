// Opens a PointMenu's menu. The menu's own script finds its list of items once the element has been rendered and
// upgraded, which can be a moment after the component's first render, so this waits for it.
//
// The menu is placed here, not left to flip itself when it doesn't fit, so that the pointer is always just inside its
// edge (over its padding, not an item). The right button was pressed before the menu opened, and a menu closes when a
// button is let go outside it after a press that began before it opened, so a menu that opened with the pointer
// outside it (as one to the left of the pointer did) closed as soon as the button was let go. It opens hidden, is
// measured, and is moved before it's shown: to the pointer's left if it doesn't fit on its right, and above it if it
// doesn't fit below.
const inset = 2;
const margin = 4;

export async function open(menuId, anchorId) {
    for (let attempt = 0; attempt < 30; attempt++) {
        const menu = document.getElementById(menuId);
        const anchor = document.getElementById(anchorId);
        const list = menu?.slottedMenuList?.[0];
        if (list && anchor) {
            const x = parseFloat(anchor.style.left);
            const y = parseFloat(anchor.style.top);
            list.style.visibility = "hidden";
            menu.openMenu();
            await new Promise(resolve => requestAnimationFrame(resolve));

            const { width, height } = list.getBoundingClientRect();
            const left = x + width > window.innerWidth - margin ? Math.max(margin, x - width + inset) : x - inset;
            const top = y + height > window.innerHeight - margin ? Math.max(margin, y - height + inset) : y - inset;
            anchor.style.left = `${left}px`;
            anchor.style.top = `${top}px`;
            await new Promise(resolve => requestAnimationFrame(resolve));
            list.style.visibility = "";
            return;
        }

        await new Promise(resolve => requestAnimationFrame(resolve));
    }
}
