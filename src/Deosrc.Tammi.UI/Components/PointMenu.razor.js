// Opens a PointMenu's menu. The menu's own script finds its list of items once the element has been rendered and
// upgraded, which can be a moment after the component's first render, so this waits for it.
export async function open(menuId) {
    for (let attempt = 0; attempt < 30; attempt++) {
        const menu = document.getElementById(menuId);
        if (menu?.slottedMenuList?.length) {
            menu.openMenu();
            return;
        }

        await new Promise(resolve => requestAnimationFrame(resolve));
    }
}
