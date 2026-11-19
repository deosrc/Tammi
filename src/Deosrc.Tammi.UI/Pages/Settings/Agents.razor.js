// Makes the image that follows the cursor while a custom agent's card is dragged a small copy of the card, not the card
// at its full size. The browser takes the image once the drag starts, from the element given to setDragImage, so a
// scaled copy is made off screen. It sits in a box of the scaled size, as the browser takes the image from the element's
// box and a transform doesn't change that.
export function useScaledDragImage(listSelector, scale) {
    const onDragStart = (event) => {
        const item = event.target.closest?.(`${listSelector} .sortable-item`);
        const card = item?.querySelector(".fluent-card") ?? item;
        if (!card || !event.dataTransfer) {
            return;
        }

        const { width, height } = card.getBoundingClientRect();
        const box = document.createElement("div");
        box.style.cssText =
            `position:fixed;top:-10000px;left:0;width:${width * scale}px;height:${height * scale}px;` +
            "overflow:hidden;pointer-events:none;";
        const copy = card.cloneNode(true);
        copy.style.cssText +=
            `;width:${width}px;margin:0;transform:scale(${scale});transform-origin:0 0;`;
        box.appendChild(copy);
        document.body.appendChild(box);

        event.dataTransfer.setDragImage(box, 12, 12);
        setTimeout(() => box.remove(), 0);
    };

    // In the capture phase, so it runs before the sortable list's own handler.
    document.addEventListener("dragstart", onDragStart, true);
    return {
        dispose() {
            document.removeEventListener("dragstart", onDragStart, true);
        },
    };
}
